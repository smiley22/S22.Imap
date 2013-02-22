using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap.Auth {
	/// <summary>
	/// A filter stream sitting between Negotiate- and NetworkStream to
	/// enable managed NTLM/GSSAPI authentication.
	/// </summary>
	/// <remarks>
	/// We use a filter for hooking into the NegotiateStream protocol rather
	/// than p/invoking SSPI directly as that would require unmanaged
	/// code privileges (internally NegotiateStream uses an SSPI wrapper).
	/// 
	/// NegotiateStream - FilterStream - NetworkStream.
	/// </remarks>
	internal class FilterStream : Stream {
		/// <summary>
		/// A buffer for accumulating handshake data until an entire handshake
		/// has been read.
		/// </summary>
		ByteBuilder handshakeData = new ByteBuilder();

		/// <summary>
		/// A buffer for accumulating the payload data following the handshake
		/// data.
		/// </summary>
		ByteBuilder payloadData = new ByteBuilder();

		/// <summary>
		/// The latest handshake header sent by the client.
		/// </summary>
		Handshake handshake;

		/// <summary>
		/// The buffer from which client reads will be satisfied.
		/// </summary>
		byte[] receivedData;

		/// <summary>
		/// The number of bytes the client has already consumed/read
		/// from the receivedData buffer.
		/// </summary>
		int receivedConsumed = 0;

		/// <summary>
		/// The current state of the filter stream.
		/// </summary>
		FilterStreamState state = FilterStreamState.ReadingHandshake;

		/// <summary>
		/// Determines whether this instance should close the inner stream
		/// when disposed.
		/// </summary>
		bool leaveOpen;

		/// <summary>
		/// An error code as specified by the NegotiateStream protocol which is
		/// handed to the NegotiateStream instance in case authentication
		/// unexpectedly fails.
		/// </summary>
		static readonly byte[] errorCode = new byte[] {
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0xFE
		};

		/// <summary>
		/// Gets the stream used by this FilterStream for sending and
		/// receiving data. 
		/// </summary>
		public Stream innerStream;

		/// <summary>
		/// Initializes a new instance of the FilterStream class using the
		/// specified Stream.
		/// </summary>
		/// <param name="s">A Stream object used by the FilterStream for sending
		/// and receiving data.</param>
		/// <param name="leaveOpen">Set to true to indicate that closing this
		/// FilterStream has no effect on innerstream, or set to false to
		/// indicate that closing this FilterStream also closes innerStream.</param>
		public FilterStream(Stream s, bool leaveOpen = false) {
			innerStream = s;
			this.leaveOpen = leaveOpen;
		}

		/// <summary>
		/// Reads data from this stream and stores it in the specified array.
		/// </summary>
		/// <param name="buffer">A byte array that receives the bytes read from
		/// the stream.</param>
		/// <param name="offset">The zero-based index into the buffer at which to
		/// begin storing the data read from this stream.</param>
		/// <param name="count">The the maximum number of bytes to read from the
		/// stream.</param>
		/// <returns>The the number of bytes read from the underlying stream. When
		/// there is no more data to be read, returns 0.</returns>
		/// <exception cref="IOException">The read operation failed.</exception>
		public override int Read(byte[] buffer, int offset, int count) {
			if (state == FilterStreamState.WaitingForServerResponse) {
				// We need to receive the server response, before we can satisfy
				// any reads.
				ReadServerResponse();
				state = FilterStreamState.SatisfyRead;
			}

			if (state != FilterStreamState.SatisfyRead)
				throw new IOException("Unexpected Read call.");
			// This really shouldn't happen but who knows.
			if (count > (receivedData.Length - receivedConsumed))
				throw new IOException("Read passed the end of received data.");
			Array.Copy(receivedData, receivedConsumed, buffer, offset, count);
			receivedConsumed += count;
			// Read buffer is empty. We don't expect another call to read for now.
			if (receivedConsumed == receivedData.Length)
					state = FilterStreamState.ReadingHandshake;
			return count;
		}

		/// <summary>
		/// Reads the server response from the underlying inner stream.
		/// </summary>
		void ReadServerResponse() {
			// We get a base64-encoded ASCII string back from the server.
			string base64 = ReadLine(innerStream);
			HandshakeType type = HandshakeType.HandshakeInProgress;
			byte[] decoded;

			try {
				// Strip off "+ " continuation command; IMAP and POP3 both use this syntax
				// whereas SMTP uses "334 ".
				base64 = Regex.Replace(base64, @"^(\+|334)\s", String.Empty);
				decoded = Convert.FromBase64String(base64);
			} catch (FormatException) {
				// If the server didn't respond with base64-data, something must have gone
				// wrong and we should gracefully shut down.
				type = HandshakeType.HandshakeError;
				decoded = errorCode;
			}

			// Prepare a new handshake to hand to the NegotiateStream instance.
			Handshake hs = new Handshake(type, (ushort) decoded.Length);
			receivedData = new ByteBuilder()
				.Append(hs.Serialize())
				.Append(decoded)
				.ToArray();
			receivedConsumed = 0;
		}

		/// <summary>
		/// Write the specified number of bytes to the underlying stream using the
		/// specified buffer and offset.
		/// </summary>
		/// <param name="buffer">A byte array that supplies the bytes written to
		/// the stream.</param>
		/// <param name="offset">The zero-based index in the buffer at which to
		/// begin reading bytes to be written to the stream.</param>
		/// <param name="count">The number of bytes to read from buffer.</param>
		/// <exception cref="IOException">The write operation failed.</exception>
		public override void Write(byte[] buffer, int offset, int count) {
			switch (state) {
				case FilterStreamState.ReadingHandshake:
					if (ReadHandshake(buffer, offset, count))
						state = FilterStreamState.ReadingPayload;
					break;
				case FilterStreamState.ReadingPayload:
					if (ReadPayload(buffer, offset, count)) {
						if (SendPayload()) {
							state = FilterStreamState.SatisfyRead;
							// Put a fake handshake into the client's receive buffer to
							// play along with the NegotiateStream protocol.
							receivedData = new Handshake(HandshakeType.HandshakeDone, 0).Serialize();
							receivedConsumed = 0;
						} else {
							state = FilterStreamState.WaitingForServerResponse;
						}
					}
					break;
				default:
					throw new IOException("Unexpected Write call.");
			}
		}

		/// <summary>
		/// Reads the client's handshake from the specified buffer.
		/// </summary>
		/// <param name="buffer">A byte array from which the handshake data
		/// will be read.</param>
		/// <param name="offset">The zero-based index in the buffer at which to
		/// begin reading bytes.</param>
		/// <param name="count">The number of bytes to read from buffer.</param>
		/// <returns>True if the handshake has been read completely, otherwise
		/// false.</returns>
		bool ReadHandshake(byte[] buffer, int offset, int count) {
			// Accumulate data into buffer until 5 bytes have been read.
			int read = Math.Min(count, 5 - handshakeData.Length);
			handshakeData.Append(buffer, offset, read);
			if (handshakeData.Length == 5) {
				// We're now expecting the payload data.
				state = FilterStreamState.ReadingPayload;

				handshake = Handshake.Deserialize(handshakeData.ToArray());
				handshakeData.Clear();
				// Append rest of buffer to payloadData.
				payloadData.Append(buffer, offset + read, count - read);
				return true;
			}
			// We haven't read 5 bytes yet.
			return false;
		}

		/// <summary>
		/// Reads the payload from the specified buffer.
		/// </summary>
		/// <param name="buffer">A byte array from which the payload data
		/// will be read.</param>
		/// <param name="offset">The zero-based index in the buffer at which to
		/// begin reading bytes.</param>
		/// <param name="count">The number of bytes to read from buffer.</param>
		/// <returns>True if all of the payload data has been read, otherwise
		/// false.</returns>
		bool ReadPayload(byte[] buffer, int offset, int count) {
			int read = Math.Min(count, handshake.PayloadSize - payloadData.Length);

			payloadData.Append(buffer, offset, read);
			// Return true once the entire payload has been read.
			return payloadData.Length == handshake.PayloadSize;
		}

		/// <summary>
		/// Sends the accumulated payload data to the server.
		/// </summary>
		/// <returns>true if the client is done sending data, otherwise
		/// false.</returns>
		bool SendPayload() {
			// IMAP (as well as Pop3 and Smtp) can't deal with binary data and
			// expect the data as Base64-encoded ASCII string terminated with a
			// CRLF.
			string base64 = Convert.ToBase64String(payloadData.ToArray());
			payloadData.Clear();

			// Send it off to the IMAP server.
			byte[] data = Encoding.ASCII.GetBytes(base64 + "\r\n");
			innerStream.Write(data, 0, data.Length);

			// If the latest client handshake is of type HandshakeDone, then the
			// client wont be sending any further handshake messages.
			return handshake.MessageId == HandshakeType.HandshakeDone;
		}

		/// <summary>
		/// Reads a line of ASCII-encoded text terminated by a CRLF from the
		/// specified stream.
		/// </summary>
		/// <param name="stream">The stream to read the line of text from.</param>
		/// <param name="resolveLiterals">Set this to true, to resolve automatically
		/// resolve possible literals.</param>
		/// <returns>A line of ASII-encoded text read from the specified
		/// stream.</returns>
		/// <remarks>"Literals" are a special feature of IMAP, employed by some
		/// server implementations. Please refer to RFC 3501 Section 4.3 for
		/// details.</remarks>
		string ReadLine(Stream stream, bool resolveLiterals = true) {
			const int Newline = 10, CarriageReturn = 13;
			using (var mem = new MemoryStream()) {
				while (true) {
					byte b = (byte) stream.ReadByte();
					if (b == CarriageReturn)
						continue;
					if (b == Newline) {
						string s = Encoding.ASCII.GetString(mem.ToArray());
						if (resolveLiterals) {
							s = Regex.Replace(s, @"{(\d+)}$", m => {
								return "\"" + ReadLiteral(stream,
									Convert.ToInt32(m.Groups[1].Value)) + "\"" +
									ReadLine(stream, false);
							});
						}
						return s;
					} else
						mem.WriteByte(b);
				}
			}
		}
		
		/// <summary>
		/// Reads the specified number of bytes from the specified stream and
		/// returns them as an ASCII-encoded string.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="byteCount">The number of bytes to read.</param>
		/// <returns>The read bytes encoded as an ASCII string.</returns>
		string ReadLiteral(Stream stream, int byteCount) {
			byte[] buffer = new byte[4096];
			using (var mem = new MemoryStream()) {
				while (byteCount > 0) {
					int request = byteCount > buffer.Length ?
						buffer.Length : byteCount;
					int read = stream.Read(buffer, 0, request);
					mem.Write(buffer, 0, read);
					byteCount = byteCount - read;
				}
				return Encoding.ASCII.GetString(mem.ToArray());
			}
		}

		/// <summary>
		/// Gets a boolean value that indicates whether the underlying stream is
		/// readable.
		/// </summary>
		public override bool CanRead {
			get {
				return true;
			}
		}

		/// <summary>
		/// Gets a boolean value that indicates whether the underlying stream is
		/// seekable.
		/// </summary>
		public override bool CanSeek {
			get {
				return false;
			}
		}

		/// <summary>
		/// Gets a boolean value that indicates whether the underlying stream
		/// supports time-outs.
		/// </summary>
		public override bool CanTimeout {
			get {
				return innerStream.CanTimeout;
			}
		}

		/// <summary>
		/// Gets a boolean value that indicates whether the underlying stream is
		/// writable.
		/// </summary>
		public override bool CanWrite {
			get {
				return true;
			}
		}

		/// <summary>
		/// Gets the length of the underlying stream.
		/// </summary>
		public override long Length {
			get {
				return innerStream.Length;
			}
		}

		/// <summary>
		/// Gets or sets the current position in the underlying stream.
		/// </summary>
		/// <exception cref="NotSupportedException">Setting this property
		/// is not supported.</exception>
		public override long Position {
			get {
				return innerStream.Position;
			}
			set {
				throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush() {
		}

		/// <summary>
		/// Throws NotSupportedException.
		/// </summary>
		/// <param name="offset">This value is ignored.</param>
		/// <param name="origin">This value is ignored.</param>
		/// <returns>Always throws a NotSupportedException.</returns>
		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotSupportedException();
		}

		/// <summary>
		/// Sets the length of the underlying stream.
		/// </summary>
		/// <param name="value">A value that specifies the length of the
		/// stream.</param>
		public override void SetLength(long value) {
			innerStream.SetLength(value);
		}

		/// <summary>
		/// Releases all resources used by the stream.
		/// </summary>
		/// <param name="disposing">True to release both managed and unmanaged
		/// resources, false to release only unmanaged resources.</param>
		protected override void Dispose(bool disposing) {
			if (!leaveOpen && innerStream != null) {
				try {
					if (disposing)
						innerStream.Close();
				} finally {
					innerStream = null;
					base.Dispose(disposing);
				}
			}
		}
	}

	/// <summary>
	/// The different states the FilterStream can be in.
	/// </summary>
	enum FilterStreamState {
		/// <summary>
		/// The stream is reading the client's handshake message.
		/// </summary>
		ReadingHandshake,
		/// <summary>
		/// The stream is reading the client's payload data.
		/// </summary>
		ReadingPayload,
		/// <summary>
		/// The stream is waiting for the server's response.
		/// </summary>
		WaitingForServerResponse,
		/// <summary>
		/// The stream has buffered the server's response and is satisfying
		/// client reads from its buffer.
		/// </summary>
		SatisfyRead
	}
}
