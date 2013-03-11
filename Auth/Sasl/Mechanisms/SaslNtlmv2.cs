using S22.Imap.Auth.Sasl.Mechanisms.Ntlm;
using System;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl NTLMv2 authentication method which addresses
	/// some of the security issues present in NTLM version 1.
	/// </summary>
	internal class SaslNtlmv2 : SaslNtlm {
		/// <summary>
		/// Private constructor for use with Sasl.SaslFactory.
		/// </summary>
		protected SaslNtlmv2() {
			// Nothing to do here.
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslNtlmv2 class
		/// using the specified username and password.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		public SaslNtlmv2(string username, string password)
			: base(username, password) {
		}

		/// <summary>
		/// Computes the client response to the specified NTLM challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server</param>
		/// <returns>The response to the NTLM challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		protected override byte[] ComputeResponse(byte[] challenge) {
			if (step == 1)
				completed = true;
			byte[] ret = step == 0 ? ComputeInitialResponse(challenge) :
				ComputeChallengeResponse(challenge);
			step = step + 1;
			return ret;
		}

		/// <summary>
		/// Computes the actual challenge response to an NTLM challenge
		/// which is sent as part of an NTLM type 2 message.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server.</param>
		/// <returns>The response to the NTLM challenge.</returns>
		/// <exception cref="SaslException">Thrown if the challenge
		/// response could not be computed.</exception>
		protected new byte[] ComputeChallengeResponse(byte[] challenge) {
			try {
				Type2Message msg = Type2Message.Deserialize(challenge);
				// This creates an NTLMv2 challenge response.
				byte[] data = new Type3Message(Username, Password, msg.Challenge,
					Username, true, msg.TargetName,
					msg.RawTargetInformation).Serialize();
				return data;
			} catch (Exception e) {
				throw new SaslException("The challenge response could not be " +
					"computed.", e);
			}
		}
	}
}
