using System;
using System.IO;
using System.Runtime.Serialization;

namespace S22.Imap.Auth {
	/// <summary>
	/// Represents a NegotiateStream handshake message.
	/// </summary>
	internal class Handshake {
		/// <summary>
		/// This is the only permissible value according to specification.
		/// </summary>
		static readonly byte majorVersion = 0x01;

		/// <summary>
		/// This is the only permissible value according to specification.
		/// </summary>
		static readonly byte minorVersion = 0x00;

		/// <summary>
		/// The type of the handshake message.
		/// </summary>
		public HandshakeType MessageId {
			get;
			set;
		}

		/// <summary>
		/// Specifies the major version of the NegotiateStream protocol
		/// being used.
		/// </summary>
		public byte MajorVersion {
			get;
			set;
		}

		/// <summary>
		/// Specifies the minor version of the NegotiateStream protocol
		/// being used.
		/// </summary>
		public byte MinorVersion {
			get;
			set;
		}

		/// <summary>
		/// Defines the size, in bytes, of the AuthPayload field, which immediately
		/// follows the handshake.
		/// </summary>
		public UInt16 PayloadSize {
			get;
			set;
		}

		/// <summary>
		/// Creates a new instance of the Handshake class using the specified type
		/// and payload size.
		/// </summary>
		/// <param name="type">The type of handshake.</param>
		/// <param name="payloadSize">The size, in bytes, of the payload following
		/// the handshake header.</param>
		public Handshake(HandshakeType type, ushort payloadSize) {
			MessageId = type;
			PayloadSize = payloadSize;
			MajorVersion = majorVersion;
			MinorVersion = minorVersion;
		}

		/// <summary>
		/// Private default constructor for deserializing.
		/// </summary>
		private Handshake() {
		}

		/// <summary>
		/// Deserializes a handshake instance from the specified byte array.
		/// </summary>
		/// <param name="data">An array of bytes containing handshake data.</param>
		/// <returns>An initialized instance of the Handshake class deserialized
		/// from the specified byte array.</returns>
		/// <exception cref="SerializationException">Thrown if the specified byte
		/// array does not contain valid handshake data.</exception>
		public static Handshake Deserialize(byte[] data) {
			Handshake hs = new Handshake();
			using (var ms = new MemoryStream(data)) {
				using (var r = new BinaryReader(ms)) {
					try {
						hs.MessageId = (HandshakeType) r.ReadByte();
						hs.MajorVersion = r.ReadByte();
						hs.MinorVersion = r.ReadByte();
						// The payload size is in network byte order (big endian).
						hs.PayloadSize = r.ReadUInt16(true);
					} catch (Exception e) {
						throw new SerializationException("The specified byte array contains " +
							"invalid data.", e);
					}
				}
			}
			// According to specification, version _must_ be 1.0
			if (hs.MajorVersion != majorVersion || hs.MinorVersion != minorVersion) {
				throw new SerializationException("Unexpected handshake version: " +
					hs.MajorVersion + "." + hs.MinorVersion);
			}
			// When the handshake message has a MessageId of HandshakeError, the
			// AuthPayload field _must_ have a length of 8 bytes.
			if (hs.MessageId == HandshakeType.HandshakeError && hs.PayloadSize != 8) {
				throw new SerializationException("Unexpected payload size. Expected " +
					"8, but was: " + hs.PayloadSize);
			}
			return hs;
		}

		/// <summary>
		/// Serializes an instance of the Handshake class to a sequence of bytes.
		/// </summary>
		/// <returns>A sequence of bytes representing this Handshake instance.</returns>
		public byte[] Serialize() {
			return new ByteBuilder()
				.Append((byte)MessageId)
				.Append(MajorVersion)
				.Append(MinorVersion)
				.Append(PayloadSize, true)
				.ToArray();
		}
	}

	/// <summary>
	/// Describes the different types of handshake messages.
	/// </summary>
	internal enum HandshakeType {
		/// <summary>
		/// The handshake has completed successfully.
		/// </summary>
		HandshakeDone = 0x14,
		/// <summary>
		/// An error occurred during the handshake. The AuthPayload field contains
		/// an HRESULT.
		/// </summary>
		HandshakeError = 0x15,
		/// <summary>
		/// The message is part of the handshake phase and is not the final message
		/// from the host. The final Handshake message from a host is always
		/// transferred in a HandshakeDone message.
		/// </summary>
		HandshakeInProgress = 0x16
	}
}
