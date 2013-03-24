using S22.Imap.Auth;
using System;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents an "octet-sequence" as is described in the SRP specification
	/// (3.3 Octet sequences, p.6).
	/// </summary>
	internal class OctetSequence {
		/// <summary>
		/// The underlying byte array forming this instance of the OctetSequence
		/// class.
		/// </summary>
		public byte[] Value {
			get;
			set;
		}

		/// <summary>
		/// Creates a new instance of the OctetSequence class using the specified
		/// byte array.
		/// </summary>
		/// <param name="sequence">The sequence of bytes to initialize this instance
		/// of the OctetSequence class with.</param>
		public OctetSequence(byte[] sequence) {
			Value = sequence;
		}

		/// <summary>
		/// Serializes this instance of the OctetSequence class into a sequence of
		/// bytes according to the requirements of the SRP specification.
		/// </summary>
		/// <returns>A sequence of bytes representing this instance of the
		/// OctetSequence class.</returns>
		/// <exception cref="OverflowException">Thrown if the length of the byte
		/// sequence exceeds the maximum number of bytes allowed as per SRP
		/// specification.</exception>
		/// <remarks>SRP specification imposes a limit of 255 bytes on the
		/// length of the underlying byte array.</remarks> 
		public byte[] Serialize() {
			byte length = Convert.ToByte(Value.Length);
			return new ByteBuilder()
				.Append(length)
				.Append(Value)
				.ToArray();
		}
	}
}
