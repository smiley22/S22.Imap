using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl Cram-Md5 authentication method as described in
	/// RFC 2831.
	/// </summary>
	internal class SaslDigestMd5 : SaslMechanism {
		bool Completed = false;

		/// <summary>
		/// The client nonce value used during authentication.
		/// </summary>
		string Cnonce = GenerateCnonce();

		/// <summary>
		/// Cram-Md5 involves several steps.
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
		/// The IANA name for the Digest-Md5 authentication mechanism as described
		/// in RFC 2195.
		/// </summary>
		public override string Name {
			get {
				return "DIGEST-MD5";
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
		/// The password to authenticate with.
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
		private SaslDigestMd5() {
			// Nothing to do here.
		}

		/// <summary>
		/// Internal constructor used for unit testing.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <param name="cnonce">The client nonce value to use.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		internal SaslDigestMd5(string username, string password, string cnonce)
			: this(username, password) {
				Cnonce = cnonce;
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslDigestMd5 class
		/// using the specified username and password.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		public SaslDigestMd5(string username, string password) {
			username.ThrowIfNull("username");
			if (username == String.Empty)
				throw new ArgumentException("The username must not be empty.");
			password.ThrowIfNull("password");

			Username = username;
			Password = password;
		}

		/// <summary>
		/// Computes the client response to the specified Digest-Md5 challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server</param>
		/// <returns>The response to the Digest-Md5 challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		protected override byte[] ComputeResponse(byte[] challenge) {
			if (Step == 1)
				Completed = true;
			// If authentication succeeded, the server responds with another
			// challenge (which we ignore) which the client must acknowledge
			// with a CRLF.
			byte[] ret = Step == 0 ? ComputeDigestResponse(challenge) :
				new byte[0];
			Step = Step + 1;
			return ret;
		}

		private byte[] ComputeDigestResponse(byte[] challenge) {
			// Precondition: Ensure username and password are not null and
			// username is not empty.
			if (String.IsNullOrEmpty(Username) || Password == null) {
				throw new SaslException("The username must not be null or empty and " +
					"the password must not be null.");
			}
			// Parse the challenge-string and construct the "response-value" from it.
			string decoded = Encoding.ASCII.GetString(challenge);
			NameValueCollection fields = ParseDigestChallenge(decoded);
			string digestUri = "imap/" + fields["realm"];
			string responseValue = ComputeDigestResponseValue(fields, Cnonce, digestUri,
				Username, Password);

			// Create the challenge-response string.
			string[] directives = new string[] {
				// We don't use UTF-8 in the current implementation.
				//"charset=utf-8",
				"username=" + Dquote(Username),
				"realm=" + Dquote(fields["realm"]),
				"nonce="+ Dquote(fields["nonce"]),
				"nc=00000001",
				"cnonce=" + Dquote(Cnonce),
				"digest-uri=" + Dquote(digestUri),
				"response=" + responseValue,
				"qop=" + fields["qop"]
			};
			string challengeResponse = String.Join(",", directives);
			// Finally, return the response as a byte array.
			return Encoding.ASCII.GetBytes(challengeResponse);
		}

		/// <summary>
		/// Parses the challenge string sent by the server in response to a Digest-Md5
		/// authentication request.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server as part of
		/// "Step One" of the Digest-Md5 authentication mechanism.</param>
		/// <returns>An initialized NameValueCollection instance made up of the
		/// attribute/value pairs contained in the challenge.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the challenge parameter
		/// is null.</exception>
		/// <remarks>Refer to RFC 2831 section 2.1.1 for a detailed description of the
		/// format of the challenge sent by the server.</remarks>
		private static NameValueCollection ParseDigestChallenge(string challenge) {
			challenge.ThrowIfNull("challenge");
			NameValueCollection coll = new NameValueCollection();
			string[] parts = challenge.Split(',');
			foreach (string p in parts) {
				string[] kv = p.Split(new char[] { '=' }, 2);
				if (kv.Length == 2)
					coll.Add(kv[0], kv[1].Trim('"'));
			}
			return coll;
		}

		/// <summary>
		/// Computes the "response-value" hex-string which is part of the
		/// Digest-MD5 challenge-response.
		/// </summary>
		/// <param name="challenge">A collection containing the attributes
		/// and values of the challenge sent by the server.</param>
		/// <param name="cnonce">The cnonce value to use for computing
		/// the response-value.</param>
		/// <param name="digestUri">The "digest-uri" string to use for
		/// computing the response-value.</param>
		/// <param name="username">The username to use for computing the
		/// response-value.</param>
		/// <param name="password">The password to use for computing the
		/// response-value.</param>
		/// <returns>A string containing a hash-value which is part of the
		/// response sent by the client.</returns>
		/// <remarks>Refer to RFC 2831, section 2.1.2.1 for a detailed
		/// description of the computation of the response-value.</remarks>
		private static string ComputeDigestResponseValue(NameValueCollection challenge,
			string cnonce, string digestUri, string username, string password) {
			// The username, realm and password are encoded with ISO-8859-1
			// (Compare RFC 2831, p. 10).
			Encoding enc = Encoding.GetEncoding("ISO-8859-1");
			string ncValue = "00000001", realm = challenge["realm"];
			// Construct A1.
			using (var md5p = new MD5CryptoServiceProvider()) {
				byte[] data = enc.GetBytes(username + ":" + realm + ":" + password);
				data = md5p.ComputeHash(data);
				string A1 = enc.GetString(data) + ":" + challenge["nonce"] + ":" +
					cnonce;
				// Construct A2.
				string A2 = "AUTHENTICATE:" + digestUri;
				if (!"auth".Equals(challenge["qop"]))
					A2 = A2 + ":00000000000000000000000000000000";
				string ret = MD5(A1, enc) + ":" + challenge["nonce"] + ":" + ncValue +
					":" + cnonce + ":" + challenge["qop"] + ":" + MD5(A2, enc);
				return MD5(ret, enc);
			}
		}

		/// <summary>
		/// Calculates the MD5 hash value for the specified string.
		/// </summary>
		/// <param name="s">The string to calculate the MD5 hash value for.</param>
		/// <param name="encoding">The encoding to employ for encoding the
		/// characters in the specified string into a sequence of bytes for
		/// which the MD5 hash will be calculated.</param>
		/// <returns>An MD5 hash as a 32-character hex-string.</returns>
		/// <exception cref="ArgumentException">Thrown if the input string
		/// is null.</exception>
		private static string MD5(string s, Encoding encoding = null) {
			if (s == null)
				throw new ArgumentNullException("s");
			if (encoding == null)
				encoding = Encoding.UTF8;
			byte[] data = encoding.GetBytes(s);
			byte[] hash = (new MD5CryptoServiceProvider()).ComputeHash(data);
			StringBuilder builder = new StringBuilder();
			foreach (byte h in hash)
				builder.Append(h.ToString("x2"));
			return builder.ToString();
		}

		/// <summary>
		/// Encloses the specified string in double-quotes.
		/// </summary>
		/// <param name="s">The string to enclose in double-quote characters.</param>
		/// <returns>The enclosed string.</returns>
		private static string Dquote(string s) {
			return "\"" + s + "\"";
		}

		/// <summary>
		/// Generates a random cnonce-value which is a "client-specified data string
		/// which must be different each time a digest-response is sent".
		/// </summary>
		/// <returns>A random "cnonce-value" string.</returns>
		private static string GenerateCnonce() {
			return Guid.NewGuid().ToString("N").Substring(0, 16);
		}
	}
}
