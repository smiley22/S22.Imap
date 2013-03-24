using System;
using System.IO;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents the second message sent by the server as part of the SRP
	/// authentication exchange.
	/// </summary>
	internal class ServerMessage2 {
		/// <summary>
		/// The evidence which proves to the client server-knowledge of the shared
		/// context key.
		/// </summary>
		public byte[] Proof {
			get;
			set;
		}

		/// <summary>
		/// The initial vector the client will use to set up its encryption
		/// context, if confidentiality protection will be employed.
		/// </summary>
		public byte[] InitialVector {
			get;
			set;
		}

		/// <summary>
		/// The session identifier the server has given to this session.
		/// </summary>
		public string SessionId {
			get;
			set;
		}

		/// <summary>
		/// The time period for which this session's parameters may be re-usable.
		/// </summary>
		public uint Ttl {
			get;
			set;
		}

		/// <summary>
		/// Deserializes a new instance of the ServerMessage2 class from the
		/// specified buffer of bytes.
		/// </summary>
		/// <param name="buffer">The byte buffer to deserialize the ServerMessage2
		/// instance from.</param>
		/// <returns>An instance of the ServerMessage2 class deserialized from the
		/// specified byte array.</returns>
		/// <exception cref="FormatException">Thrown if the byte buffer does not
		/// contain valid data.</exception>
		public static ServerMessage2 Deserialize(byte[] buffer) {
			using (var ms = new MemoryStream(buffer)) {
				using (var r = new BinaryReader(ms)) {
					uint bufferLength = r.ReadUInt32(true);
					OctetSequence M2 = r.ReadOs(),
						sIV = r.ReadOs();
					Utf8String sid = r.ReadUtf8String();
					uint ttl = r.ReadUInt32(true);
					return new ServerMessage2() {
						Proof = M2.Value,
						InitialVector = sIV.Value,
						SessionId = sid.Value,
						Ttl = ttl
					};
				}
			}
		}
	}
}
