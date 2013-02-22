using System;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Represents an NTLM Type 3 Message.
	/// </summary>
	internal class Type3Message {
		/// <summary>
		/// The NTLM message signature which is always "NTLMSSP".
		/// </summary>
		static readonly string signature = "NTLMSSP";

		/// <summary>
		/// The NTML message type which is always 3 for an NTLM Type 3 message.
		/// </summary>
		static readonly MessageType type = MessageType.Type3;

		/// <summary>
		/// The NTLM flags set on this instance.
		/// </summary>
		public Flags Flags {
			get;
			set;
		}

		/// <summary>
		/// The "Lan Manager" challenge response.
		/// </summary>
		byte[] LMResponse {
			get;
			set;
		}

		/// <summary>
		/// The offset at which the LM challenge response data starts.
		/// </summary>
		int LMOffset {
			get {
				// We send a version 3 NTLM type 3 message so the start of the data
				// block is at offset 72.
				return 72;
			}
		}

		/// <summary>
		/// The NTLM challenge response.
		/// </summary>
		byte[] NtlmResponse {
			get;
			set;
		}

		/// <summary>
		/// The offset at which the NTLM challenge response data starts.
		/// </summary>
		int NtlmOffset {
			get {
				return LMOffset + LMResponse.Length;
			}
		}

		/// <summary>
		/// The authentication realm in which the authenticating account
		/// has membership.
		/// </summary>
		byte[] targetName {
			get;
			set;
		}

		/// <summary>
		/// The offset at which the target name data starts.
		/// </summary>
		int targetOffset {
			get {
				return NtlmOffset + NtlmResponse.Length;
			}
		}

		/// <summary>
		/// The authenticating account name.
		/// </summary>
		byte[] username {
			get;
			set;
		}

		/// <summary>
		/// The offset at which the username data starts.
		/// </summary>
		int usernameOffset {
			get {
				return targetOffset + targetName.Length;
			}
		}

		/// <summary>
		/// The client workstation's name.
		/// </summary>
		byte[] workstation {
			get;
			set;
		}

		/// <summary>
		/// The offset at which the client workstation's name data starts.
		/// </summary>
		int workstationOffset {
			get {
				return usernameOffset + username.Length;
			}
		}

		/// <summary>
		/// The session key value which is used by the session security mechanism
		/// during key exchange.
		/// </summary>
		byte[] sessionKey {
			get;
			set;
		}

		/// <summary>
		/// The offset at which the session key data starts.
		/// </summary>
		int sessionKeyOffset {
			get {
				return workstationOffset + workstation.Length;
			}
		}

		/// <summary>
		/// Contains the data present in the OS version structure.
		/// </summary>
		OSVersion OSVersion {
			get;
			set;
		}

		/// <summary>
		/// The encoding used for transmitting the contents of the various
		/// security buffers.
		/// </summary>
		Encoding encoding {
			get;
			set;
		}

		/// <summary>
		/// Creates a new instance of an NTLM type 3 message using the specified
		/// values.
		/// </summary>
		/// <param name="username">The Windows account name to use for
		/// authentication.</param>
		/// <param name="password">The Windows account password to use for
		/// authentication.</param>
		/// <param name="challenge">The challenge received from the server as part
		/// of the NTLM type 2 message.</param>
		/// <param name="workstation">The client's workstation name.</param>
		/// <param name="ntlmv2">Set to true to send an NTLMv2 challenge
		/// response.</param>
		/// <param name="targetName">The authentication realm in which the
		/// authenticating account has membership.</param>
		/// <param name="targetInformation">The target information block from
		/// the NTLM type 2 message.</param>
		/// <remarks>The target name is a domain name for domain accounts, or
		/// a server name for local machine accounts. All security buffers will
		/// be encoded as Unicode.</remarks>
		public Type3Message(string username, string password, byte[] challenge,
			string workstation, bool ntlmv2 = false, string targetName = null,
			byte[] targetInformation = null)
			: this(username, password, challenge, true, workstation, ntlmv2,
					targetName, targetInformation)
		{
		}

		/// <summary>
		/// Creates a new instance of an NTLM type 3 message using the specified
		/// values.
		/// </summary>
		/// <param name="username">The Windows account name to use for
		/// authentication.</param>
		/// <param name="password">The Windows account password to use for
		/// authentication.</param>
		/// <param name="challenge">The challenge received from the server as part
		/// of the NTLM type 2 message.</param>
		/// <param name="useUnicode">Set this to true, if Unicode encoding has been
		/// negotiated between client and server.</param>
		/// <param name="workstation">The client's workstation name.</param>
		/// <param name="ntlmv2">Set to true to send an NTLMv2 challenge
		/// response.</param> 
		/// <param name="targetName">The authentication realm in which the
		/// authenticating account has membership.</param>
		/// <param name="targetInformation">The target information block from
		/// the NTLM type 2 message.</param>
		/// <remarks>The target name is a domain name for domain accounts, or
		/// a server name for local machine accounts.</remarks>
		/// <exception cref="ArgumentNullException">Thrown if the username, password
		/// or challenge parameters are null.</exception>
		public Type3Message(string username, string password, byte[] challenge,
			bool useUnicode, string workstation, bool ntlmv2 = false,
			string targetName = null, byte[] targetInformation = null) {
			// Preconditions.
			username.ThrowIfNull("username");
			password.ThrowIfNull("password");
			challenge.ThrowIfNull("challenge");
			encoding = useUnicode ? Encoding.Unicode : Encoding.ASCII;

			// Setup the security buffers contents.
			this.username = encoding.GetBytes(username);
			this.workstation = encoding.GetBytes(workstation);
			this.targetName = String.IsNullOrEmpty(targetName) ? new byte[0] :
				encoding.GetBytes(targetName);
			// The session key is not relevant to authentication.
			this.sessionKey = new byte[0];
			// Compute the actual challenge response data.
			if (!ntlmv2) {
				LMResponse = Responses.ComputeLMResponse(challenge, password);
				NtlmResponse = Responses.ComputeNtlmResponse(challenge, password);
			} else {
				byte[] cnonce = GetCNonce();
				LMResponse = Responses.ComputeLMv2Response(targetName, username,
					password, challenge, cnonce);
				NtlmResponse = Responses.ComputeNtlmv2Response(targetName,
					username, password, targetInformation, challenge, cnonce);
			}
			// We spoof an OS version of Windows 7 Build 7601.
			OSVersion = new OSVersion(6, 1, 7601);
		}

		/// <summary>
		/// Serializes this instance of the Type3 class to an array of bytes.
		/// </summary>
		/// <returns>An array of bytes representing this instance of the Type3
		/// class.</returns>
		public byte[] Serialize() {
			return new ByteBuilder()
				.Append(signature + "\0")
				.Append((int) type)
				.Append(new SecurityBuffer(LMResponse, LMOffset).Serialize())
				.Append(new SecurityBuffer(NtlmResponse, NtlmOffset).Serialize())
				.Append(new SecurityBuffer(targetName, targetOffset).Serialize())
				.Append(new SecurityBuffer(username, usernameOffset).Serialize())
				.Append(new SecurityBuffer(workstation, workstationOffset).Serialize())
				.Append(new SecurityBuffer(sessionKey, sessionKeyOffset).Serialize())
				.Append((int) Flags)
				.Append(OSVersion.Serialize())
				.Append(LMResponse)
				.Append(NtlmResponse)
				.Append(targetName)
				.Append(username)
				.Append(workstation)
				.Append(sessionKey)
				.ToArray();
		}

		/// <summary>
		/// Returns a random 8-byte cnonce value.
		/// </summary>
		/// <returns>A random 8-byte cnonce value.</returns>
		private static byte[] GetCNonce() {
			byte[] b = new byte[8];
			new Random().NextBytes(b);
			return b;
		}
	}
}
