using System;
using System.Collections.Specialized;
using System.IO;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents the first message sent by the server in response to an
	/// initial client-response.
	/// </summary>
	internal class ServerMessage1 {
		/// <summary>
		/// The safe prime modulus sent by the server.
		/// </summary>
		public Mpi SafePrimeModulus {
			get;
			set;
		}

		/// <summary>
		/// The generator sent by the server.
		/// </summary>
		public Mpi Generator {
			get;
			set;
		}

		/// <summary>
		/// The user's password salt.
		/// </summary>
		public byte[] Salt {
			get;
			set;
		}

		/// <summary>
		/// The server's ephemeral public key.
		/// </summary>
		public Mpi PublicKey {
			get;
			set;
		}

		/// <summary>
		/// The options list indicating available security services.
		/// </summary>
		public NameValueCollection Options {
			get;
			set;
		}

		/// <summary>
		/// The raw options as received from the server.
		/// </summary>
		public string RawOptions {
			get;
			set;
		}

		/// <summary>
		/// Deserializes a new instance of the ServerMessage1 class from the
		/// specified buffer of bytes.
		/// </summary>
		/// <param name="buffer">The byte buffer to deserialize the ServerMessage1
		/// instance from.</param>
		/// <returns>An instance of the ServerMessage1 class deserialized from the
		/// specified byte array.</returns>
		/// <exception cref="FormatException">Thrown if the byte buffer does not
		/// contain valid data.</exception>
		public static ServerMessage1 Deserialize(byte[] buffer) {
			using (var ms = new MemoryStream(buffer)) {
				using (var r = new BinaryReader(ms)) {
					uint bufferLength = r.ReadUInt32(true);
					// We don't support re-using previous sessions.
					byte reuse = r.ReadByte();
					if (reuse != 0) {
						throw new FormatException("Unexpected re-use parameter value: " +
							reuse);
					}
					Mpi N = r.ReadMpi();
					Mpi g = r.ReadMpi();
					OctetSequence salt = r.ReadOs();
					Mpi B = r.ReadMpi();
					Utf8String L = r.ReadUtf8String();
					return new ServerMessage1() {
						Generator = g,
						PublicKey = B,
						Salt = salt.Value,
						SafePrimeModulus = N,
						Options = ParseOptions(L.Value),
						RawOptions = L.Value
					};
				}
			}
		}

		/// <summary>
		/// Parses the options string sent by the server.
		/// </summary>
		/// <param name="s">A comma-delimited options string.</param>
		/// <returns>An initialized instance of the NameValueCollection class
		/// containing the parsed server options.</returns>
		public static NameValueCollection ParseOptions(string s) {
			NameValueCollection coll = new NameValueCollection();
			string[] parts = s.Split(',');
			foreach (string p in parts) {
				int index = p.IndexOf('=');
				if (index < 0) {
					coll.Add(p, "true");
				} else {
					string name = p.Substring(0, index), value = p.Substring(index + 1);
					coll.Add(name, value);
				}
			}
			return coll;
		}
	}
}
