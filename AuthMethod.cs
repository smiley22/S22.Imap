using System;

namespace S22.Imap {
	/// <summary>
	/// Defines supported means of authenticating with the IMAP server.
	/// </summary>
	public enum AuthMethod {
		/// <summary>
		/// Login using plaintext password authentication. This is
		/// the default supported by most servers.
		/// </summary>
		Login,
		/// <summary>
		/// Login using the CRAM-MD5 authentication mechanism.
		/// </summary>
		CRAMMD5,
		/// <summary>
		/// Login using the OAuth authentication mechanism over
		/// the Simple Authentication and Security Layer (Sasl).
		/// </summary>
		SaslOAuth
	}
}
