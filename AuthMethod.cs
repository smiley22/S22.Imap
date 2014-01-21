using System;

namespace S22.Imap {
	/// <summary>
	/// Defines supported means of authenticating with an IMAP server.
	/// </summary>
	public enum AuthMethod {
		/// <summary>
		/// Automatically selects the most-secure authentication mechanism supported by the server.
		/// </summary>
		Auto,
		/// <summary>
		/// Login using plaintext password authentication; This is supported by most servers.
		/// </summary>
		Login,
		/// <summary>
		/// Login using the SASL PLAIN authentication mechanism.
		/// </summary>
		Plain,
		/// <summary>
		/// Login using the CRAM-MD5 authentication mechanism.
		/// </summary>
		CramMd5,
		/// <summary>
		/// Login using the DIGEST-MD5 authentication mechanism.
		/// </summary>
		DigestMd5,
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
		/// Login using the NTLM/NTLMv2 authentication mechanism via Microsoft's Security Support
		/// Provider Interface (SSPI).
		/// </summary>
		NtlmOverSspi,
		/// <summary>
		/// Login using Kerberos authentication via the SASL GSSAPI mechanism.
		/// </summary>
		Gssapi,
		/// <summary>
		/// Login using the SCRAM-SHA-1 authentication mechanism.
		/// </summary>
		ScramSha1,
		/// <summary>
		/// Login using the Secure Remote Password (SRP) authentication mechanism.
		/// </summary>
		/// <remarks>The SRP mechanism is only available when targeting .NET 4.0 or newer.</remarks>
		Srp
	}
}
