using System;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;

namespace S22.Imap {
	/// <summary>
	/// Contains methods for computing challenge-responses for the various
	/// authentication mechanisms.
	/// </summary>
	internal static class Authentication {
		/// <summary>
		/// Computes the client response for a PLAIN challenge sent by the
		/// server.
		/// </summary>
		/// <param name="username">The username to use for constructing the
		/// challenge-reponse.</param>
		/// <param name="password">The username to use for constructing the
		/// challenge-response.</param>
		/// <returns>A base64-encoded string containing the response to the
		/// challenge.</returns>
		public static string Plain(string username, string password) {
			// Username and password are delimited by a NUL (U+0000) character.
			string s = "\0" + username + "\0" + password;
			// The response is a UTF-8 string, encoded as base64. 
			byte[] data = Encoding.UTF8.GetBytes(s);
			return Convert.ToBase64String(data);
		}

		/// <summary>
		/// Computes the client response for a CRAM-MD5 challenge sent by the
		/// server.
		/// </summary>
		/// <param name="challenge">The base64-encoded challenge received from the
		/// server.</param>
		/// <param name="username">The username to use for constructing the
		/// challenge-reponse.</param>
		/// <param name="password">The username to use for constructing the
		/// challenge-response.</param>
		/// <returns>A base64-encoded string containing the response to the
		/// challenge.</returns>
		/// <exception cref="FormatException">Thrown if the specified challenge
		/// is not a valid base-64 encoded challenge-string.</exception>
		public static string CramMD5(string challenge, string username,
			string password) {
			// Precondition.
			challenge.ThrowIfNull("challenge");
			username.ThrowIfNull("username");
			password.ThrowIfNull("password");
			// Decode the Base64-encoded challenge.
			byte[] decoded = Convert.FromBase64String(challenge);
			// Compute the encrypted challenge as a hex-string.
			string hex = String.Empty;
			using (var hmac = new HMACMD5(Encoding.ASCII.GetBytes(password))) {
				byte[] encrypted = hmac.ComputeHash(decoded);
				hex = BitConverter.ToString(encrypted).ToLower().Replace("-",
					String.Empty);
			}
			// Construct the Base64-encoded response.
			byte[] data = Encoding.ASCII.GetBytes(username + " " + hex);			
			return Convert.ToBase64String(data);
		}

		/// <summary>
		/// Computes the "digest-response" for a DIGEST-MD5 challenge sent by the
		/// server.
		/// </summary>
		/// <param name="challenge">The base64-encoded challenge received from the
		/// server.</param>
		/// <param name="username">The username to use for constructing the
		/// challenge-reponse.</param>
		/// <param name="password">The username to use for constructing the
		/// challenge-response.</param>
		/// <returns>A base64-encoded string containing the response to the
		/// challenge.</returns>
		/// <exception cref="FormatException">Thrown if the specified challenge
		/// is not a valid base-64 encoded challenge-string.</exception>
		public static string DigestMD5(string challenge, string username, string password) {
			// Precondition.
			challenge.ThrowIfNull("challenge");
			username.ThrowIfNull("username");
			password.ThrowIfNull("password");
			// Decode the Base64-encoded challenge.
			string decoded = Encoding.ASCII.GetString(Convert.FromBase64String(challenge));
			// Parse the challenge-string and construct the "response-value" from it.
			NameValueCollection fields = ParseDigestChallenge(decoded);
			string cnonce = GenerateCnonce(),
				digestUri = "imap/" + fields["realm"];
			string responseValue = ComputeDigestResponseValue(fields, cnonce, digestUri,
				username, password);

			// Create the challenge-response string.
			string[] directives = new string[] {
				// We don't use UTF-8 in the current implementation.
				//"charset=utf-8",
				"username=" + Dquote(username),
				"realm=" + Dquote(fields["realm"]),
				"nonce="+ Dquote(fields["nonce"]),
				"nc=00000001",
				"cnonce=" + Dquote(cnonce),
				"digest-uri=" + Dquote(digestUri),
				"response=" + responseValue,
				"qop=" + fields["qop"]
			};
			string challengeResponse = String.Join(",", directives);
			// Finally, return the response as a base64-encoded string as is expected
			// by the server.
			return Convert.ToBase64String(Encoding.ASCII.GetBytes(challengeResponse));
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
		/// Generates a random cnonce-value which is a "client-specified data string
		/// which must be different each time a digest-response is sent".
		/// </summary>
		/// <returns>A random "cnonce-value" string.</returns>
		private static string GenerateCnonce() {
			return Guid.NewGuid().ToString("N").Substring(0, 16);
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
		/// <remarks>Needed by the DIGEST-MD5 mechanism.</remarks>
		private static string Dquote(string s) {
			return "\"" + s + "\"";
		}

	}
}
