using System;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl OAuth 2.0 authentication method.
	/// </summary>
	internal class SaslOAuth2 : SaslMechanism {
		bool Completed = false;

		/// <summary>
		/// The server sends an error response in case authentication fails
		/// which must be acknowledged.
		/// </summary>
		int Step = 0;

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
		/// The IANA name for the OAuth 2.0 authentication mechanism.
		/// </summary>
		public override string Name {
			get {
				return "XOAUTH2";
			}
		}

		/// <summary>
		/// The username to authenticate with.
		/// </summary>
		string Username {
			get {
				return Properties.ContainsKey("Username") ?
					Properties["Username"] as string : null;
			}
			set {
				Properties["Username"] = value;
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
		private SaslOAuth2() {
			// Nothing to do here.
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslOAuth class
		/// using the specified username and password.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="accessToken">The username to authenticate with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the accessToken parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username or
		/// the accessToken parameter is empty.</exception>
		public SaslOAuth2(string username, string accessToken) {
			username.ThrowIfNull("username");
			accessToken.ThrowIfNull("accessToken");
			if (username == String.Empty)
				throw new ArgumentException("The username must not be empty.");
			if(accessToken == String.Empty)
				throw new ArgumentException("The access token must not be empty.");
			Username = username;
			AccessToken = accessToken;
		}

		/// <summary>
		/// Computes the client response to an XOAUTH2 challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server.</param>
		/// <returns>The response to the OAuth2 challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		protected override byte[] ComputeResponse(byte[] challenge) {
			if (Step == 1)
				Completed = true;
			// If authentication fails, the server responds with another
			// challenge (error response) which the client must acknowledge
			// with a CRLF.
			byte[] ret = Step == 0 ? ComputeInitialResponse(challenge) :
				new byte[0];
			Step = Step + 1;
			return ret;
		}

		/// <summary>
		/// Computes the initial client response to an XOAUTH2 challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server.</param>
		/// <returns>The response to the OAuth2 challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		private byte[] ComputeInitialResponse(byte[] challenge) {
			// Precondition: Ensure access token is not null and is not empty.
			if (String.IsNullOrEmpty(Username) || String.IsNullOrEmpty(AccessToken)) {
				throw new SaslException("The username and access token must not be" +
					" null or empty.");
			}
			// ^A = Control A = (U+0001)
			char A = '\u0001';
			string s = "user=" + Username + A + "auth=Bearer " + AccessToken + A + A;
			// The response is encoded as ASCII. 
			return Encoding.ASCII.GetBytes(s);
		}
	}
}
