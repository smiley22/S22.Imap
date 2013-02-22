using System;

namespace S22.Imap {
	/// <summary>
	/// Defines supported means of authenticating with an IMAP server.
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
		DigestMD5,
		/// <summary>
		/// Login using OAuth via the SASL XOAuth mechanism.
		/// </summary>
		OAuth,
		/// <summary>
		/// Login using OAuth 2.0 via the SASL XOAUTH2 mechanism.
		/// </summary>
		OAuth2,
		/// <summary>
		/// Login using the NTLM authentication mechanism.
		/// </summary>
		Ntlm,
		/// <summary>
		/// Login using the NTLMv2 authentication mechanism.
		/// </summary>
		Ntlmv2,
		/// <summary>
		/// Login using the NTLM/NTLMv2 authentication mechanism via
		/// Microsoft's Security Support Provider Interface (SSPI).
		/// </summary>
		NtlmOverSspi,
		/// <summary>
		/// Login using Kerberos authentication via the SASL GSSAPI mechanism.
		/// </summary>
		Gssapi
	}
}
