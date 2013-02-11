using System;

namespace S22.Imap {
	/// <summary>
	/// Defines supported means of authenticating with the IMAP server.
	/// </summary>
	public enum AuthMethod {
		/// <summary>
		/// Automatically selects the most-secure authentication mechanism
		/// supported by the server.
		/// </summary>
		Auto,
		/// <summary>
		/// Login using plaintext password authentication. This is
		/// the default supported by most servers.
		/// </summary>
		Login,
		/// <summary>
		/// Login using the SASL PLAIN authentication mechanism.
		/// </summary>
		Plain,
		/// <summary>
		/// Login using the CRAM-MD5 authentication mechanism.
		/// </summary>
		CramMD5,
		/// <summary>
		/// Login using the DIGEST-MD5 authentication mechanism.
		/// </summary>
		DigestMD5
	}
}
