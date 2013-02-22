using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl Plain authentication method as described in
	/// RFC 4616.
	/// </summary>
	internal class SaslPlain : SaslMechanism {
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
		/// The IANA name for the Plain authentication mechanism as described
		/// in RFC 4616.
		/// </summary>
		public override string Name {
			get {
				return "PLAIN";
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
		/// The plain-text password to authenticate with.
		/// </summary>
		string Password {
			get {
				return Properties.ContainsKey("Password") ?
					Properties["Password"] as string : null;
			}
			set {
				Properties["Password"] = value;
			}
		}

		/// <summary>
		/// Private constructor for use with Sasl.SaslFactory.
		/// </summary>
		private SaslPlain() {
			// Nothing to do here.
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslPlain class
		/// using the specified username and password.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		public SaslPlain(string username, string password) {
			username.ThrowIfNull("username");
			if (username == String.Empty)
				throw new ArgumentException("The username must not be empty.");
			password.ThrowIfNull("password");

			Username = username;
			Password = password;
		}

		/// <summary>
		/// Computes the client response for a plain-challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server. For the
		/// "plain" mechanism this will usually be empty.</param>
		/// <returns>The response for the "plain"-challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		protected override byte[] ComputeResponse(byte[] challenge) {
			// Precondition: Ensure username and password are not null and
			// username is not empty.
			if (String.IsNullOrEmpty(Username) || Password == null) {
				throw new SaslException("The username must not be null or empty and " +
					"the password must not be null.");
			}
			// Sasl Plain does not involve another roundtrip.
			Completed = true;
			// Username and password are delimited by a NUL (U+0000) character
			// and the response shall be encoded as UTF-8.
			return Encoding.UTF8.GetBytes("\0" + Username + "\0" + Password);
		}
	}
}
