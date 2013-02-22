using System;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl OAuth authentication method.
	/// </summary>
	internal class SaslOAuth : SaslMechanism {
		bool Completed = false;

		/// <summary>
		/// True if the authentication exchange between client and server
		/// has been completed.
		/// </summary>
		public override bool IsCompleted {
			get {
				return Completed;
			}
		}

		/// <summary>
		/// The IANA name for the OAuth authentication mechanism.
		/// </summary>
		public override string Name {
			get {
				return "XOAUTH";
			}
		}

		/// <summary>
		/// The access token to authenticate with.
		/// </summary>
		string AccessToken {
			get {
				return Properties.ContainsKey("AccessToken") ?
					Properties["AccessToken"] as string : null;
			}
			set {
				Properties["AccessToken"] = value;
			}
		}

		/// <summary>
		/// Private constructor for use with Sasl.SaslFactory.
		/// </summary>
		private SaslOAuth() {
			// Nothing to do here.
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslOAuth class
		/// using the specified username and password.
		/// </summary>
		/// <param name="accessToken">The username to authenticate with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the accessToken
		/// parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the accessToken
		/// parameter is empty.</exception>
		public SaslOAuth(string accessToken) {
			accessToken.ThrowIfNull("accessToken");
			if (accessToken == String.Empty)
				throw new ArgumentException("The access token must not be empty.");

			AccessToken = accessToken;
		}

		/// <summary>
		/// Computes the client response for a OAuth challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server.</param>
		/// <returns>The response to the OAuth challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		protected override byte[] ComputeResponse(byte[] challenge) {
			// Precondition: Ensure access token is not null and is not empty.
			if (String.IsNullOrEmpty(AccessToken))
				throw new SaslException("The access token must not be null or empty.");

			// Sasl OAuth does not involve another roundtrip.
			Completed = true;
			return Encoding.ASCII.GetBytes(AccessToken);
		}
	}
}
