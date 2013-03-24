using System;
using System.Text;

namespace S22.Imap.Auth {
	/// <summary>
	/// A utility class modeled after the BCL StringBuilder to simplify
	/// building binary-data messages.
	/// </summary>
	internal class ByteBuilder {
		/// <summary>
		/// The actual byte buffer.
		/// </summary>
		byte[] buffer = new byte[1024];

		/// <summary>
		/// The current position in the buffer.
		/// </summary>
		int position = 0;

		/// <summary>
		/// The length of the underlying data buffer.
		/// </summary>
		public int Length {
			get {
				return position;
			}
		}

		/// <summary>
		/// Resizes the internal byte buffer.
		/// </summary>
		/// <param name="amount">Amount in bytes by which to increase the
		/// size of the buffer.</param>
		void Resize(int amount = 1024) {
			byte[] newBuffer = new byte[buffer.Length + amount];
			Array.Copy(buffer, newBuffer, buffer.Length);
			buffer = newBuffer;
		}

		/// <summary>
		/// Appends one or several byte values to this instance.
		/// </summary>
		/// <param name="values">Byte values to append.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(params byte[] values) {
			if ((position + values.Length) >= buffer.Length)
				Resize();
			foreach (byte b in values)
				buffer[position++] = b;
			return this;
		}

		/// <summary>
		/// Appends the specified number of bytes from the specified buffer
		/// starting at the specified offset to this instance.
		/// </summary>
		/// <param name="buffer">The buffer to append bytes from.</param>
		/// <param name="offset">The offset into the buffert at which to start
		/// reading bytes from.</param>
		/// <param name="count">The number of bytes to read from the buffer.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(byte[] buffer, int offset, int count) {
			if ((position + count) >= buffer.Length)
				Resize();
			for (int i = 0; i < count; i++)
				this.buffer[position++] = buffer[offset + i];
			return this;
		}

		/// <summary>
		/// Appends the specified 32-bit integer value to this instance.
		/// </summary>
		/// <param name="value">A 32-bit integer value to append.</param>
		/// <param name="bigEndian">Set this to true, to append the value as
		/// big-endian.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(int value, bool bigEndian = false) {
			if ((position + 4) >= buffer.Length)
				Resize();
			int[] o = bigEndian ? new int[4] { 3, 2, 1, 0 } :
				new int[4] { 0, 1, 2, 3 };
			for (int i = 0; i < 4; i++)
				buffer[position++] = (byte) ((value >> (o[i] * 8)) & 0xFF);
			return this;
		}

		/// <summary>
		/// Appends the specified 16-bit short value to this instance.
		/// </summary>
		/// <param name="value">A 16-bit short value to append.</param>
		/// <param name="bigEndian">Set this to true, to append the value as
		/// big-endian.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(short value, bool bigEndian = false) {
			if ((position + 2) >= buffer.Length)
				Resize();
			int[] o = bigEndian ? new int[2] { 1, 0 } :
				new int[2] { 0, 1 };
			for (int i = 0; i < 2; i++)
				buffer[position++] = (byte) ((value >> (o[i] * 8)) & 0xFF);
			return this;
		}

		/// <summary>
		/// Appends the specified 16-bit unsigend short value to this instance.
		/// </summary>
		/// <param name="value">A 16-bit unsigend short value to append.</param>
		/// <param name="bigEndian">Set this to true, to append the value as
		/// big-endian.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(ushort value, bool bigEndian = false) {
			if ((position + 2) >= buffer.Length)
				Resize();
			int[] o = bigEndian ? new int[2] { 1, 0 } :
				new int[2] { 0, 1 };
			for (int i = 0; i < 2; i++)
				buffer[position++] = (byte) ((value >> (o[i] * 8)) & 0xFF);
			return this;
		}

		/// <summary>
		/// Appends the specified 32-bit unsigned integer value to this instance.
		/// </summary>
		/// <param name="value">A 32-bit unsigned integer value to append.</param>
		/// <param name="bigEndian">Set this to true, to append the value as
		/// big-endian.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(uint value, bool bigEndian = false) {
			if ((position + 4) >= buffer.Length)
				Resize();
			int[] o = bigEndian ? new int[4] { 3, 2, 1, 0 } :
				new int[4] { 0, 1, 2, 3 };
			for (int i = 0; i < 4; i++)
				buffer[position++] = (byte) ((value >> (o[i] * 8)) & 0xFF);
			return this;
		}

		/// <summary>
		/// Appends the specified 64-bit integer value to this instance.
		/// </summary>
		/// <param name="value">A 64-bit integer value to append.</param>
		/// <param name="bigEndian">Set this to true, to append the value as
		/// big-endian.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(long value, bool bigEndian = false) {
			if ((position + 8) >= buffer.Length)
				Resize();
			int[] o = bigEndian ? new int[8] { 7, 6, 5, 4, 3, 2, 1, 0 } :
				new int[8] { 0, 1, 2, 3, 4, 5, 6, 7 };
			for (int i = 0; i < 8; i++)
				buffer[position++] = (byte) ((value >> (o[i] * 8)) & 0xFF);
			return this;

		}

		/// <summary>
		/// Appends the specified string using the specified encoding to this
		/// instance.
		/// </summary>
		/// <param name="value">The string vale to append.</param>
		/// <param name="encoding">The encoding to use for decoding the string value
		/// into a sequence of bytes. If this is null, ASCII encoding is used as a
		/// default.</param>
		/// <returns>A reference to the calling instance.</returns>
		public ByteBuilder Append(string value, Encoding encoding = null) {
			if (encoding == null)
				encoding = Encoding.ASCII;
			byte[] bytes = encoding.GetBytes(value);
			if ((position + bytes.Length) >= buffer.Length)
				Resize();
			foreach (byte b in bytes)
				buffer[position++] = b;
			return this;
		}

		/// <summary>
		/// Returns the ByteBuilder's content as an array of bytes.
		/// </summary>
		/// <returns>An array of bytes.</returns>
		public byte[] ToArray() {
			// Fixme: Do this properly.
			byte[] b = new byte[position];
			Array.Copy(buffer, b, position);
			return b;
		}

		/// <summary>
		/// Removes all bytes from the current ByteBuilder instance.
		/// </summary>
		public void Clear() {
			buffer = new byte[1024];
			position = 0;
		}
	}
}
