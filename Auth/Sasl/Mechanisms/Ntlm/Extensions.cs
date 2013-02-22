using S22.Imap.Auth;
using System;
using System.IO;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Adds extension methods to the BinaryReader class to simplify the
	/// deserialization of NTLM messages.
	/// </summary>
	internal static class BinaryReaderExtensions {
		/// <summary>
		/// Reads an ASCII-string of the specified length from this instance.
		/// </summary>
		/// <param name="reader">Extension method for the BinaryReader class.</param>
		/// <param name="count">The number of bytes to read from the underlying
		/// stream.</param>
		/// <returns>A string decoded from the bytes read from the underlying
		/// stream using the ASCII character set.</returns>
		public static string ReadASCIIString(this BinaryReader reader, int count) {
			ByteBuilder builder = new ByteBuilder();
			int read = 0;
			while (true) {
				if (read++ >= count)
					break;
				byte b = reader.ReadByte();
				builder.Append(b);
			}
			return Encoding.ASCII.GetString(builder.ToArray()).TrimEnd('\0');
		}
	}
}
