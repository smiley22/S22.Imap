using S22.Imap.Auth;
using System;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents an UTF-8 string as is described in the SRP specification
	/// (3.5 Text, p.6).
	/// </summary>
	internal class Utf8String {
		/// <summary>
		/// The value of the UTF-8 string.
		/// </summary>
		public string Value;

		/// <summary>
		/// Creates a new instance of the Utf8String class using the specified
		/// string value.
		/// </summary>
		/// <param name="s">The string to initialize the Utf8String instance
		/// with.</param>
		public Utf8String(string s) {
			Value = s;
		}

		/// <summary>
		/// Serializes this instance of the Utf8String class into a sequence of
		/// bytes according to the requirements of the SRP specification.
		/// </summary>
		/// <returns>A sequence of bytes representing this instance of the
		/// Utf8String class.</returns>
		/// <exception cref="OverflowException">Thrown if the string value exceeds
		/// the maximum number of bytes allowed as per SRP specification.</exception>
		/// <remarks>SRP specification imposes a limit of 65535 bytes on the
		/// string data after it has been encoded into a sequence of bytes
		/// using an encoding of UTF-8.</remarks> 
		public byte[] Serialize() {
			byte[] b = Encoding.UTF8.GetBytes(Value);
			ushort length = Convert.ToUInt16(b.Length);
			return new ByteBuilder()
				.Append(length, true)
				.Append(b)
				.ToArray();
		}
	}
}
