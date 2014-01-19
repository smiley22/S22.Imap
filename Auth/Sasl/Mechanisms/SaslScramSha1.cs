using System;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl SCRAM-SHA-1 authentication method as described in
	/// RFC 5802.
	/// </summary>
	internal class SaslScramSha1 : SaslMechanism {
		bool Completed = false;

		/// <summary>
		/// The client nonce value used during authentication.
		/// </summary>
		string Cnonce = GenerateCnonce();

		/// <summary>
		/// Scram-Sha-1 involves several steps.
		/// </summary>
		int Step = 0;

		/// <summary>
		/// The salted password. This is needed for client authentication and later
		/// on again for verifying the server signature.
		/// </summary>
		byte[] SaltedPassword;

		/// <summary>
		/// The auth message is part of the authentication exchange and is needed for
		/// authentication as well as for verifying the server signature.
		/// </summary>
		string AuthMessage;

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
		/// The IANA name for the Scram-Sha-1 authentication mechanism as described
		/// in RFC 5802.
		/// </summary>
		public override string Name {
			get {
				return "SCRAM-SHA-1";
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
		private SaslScramSha1() {
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
		internal SaslScramSha1(string username, string password, string cnonce)
			: this(username, password) {
				Cnonce = cnonce;
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslScramSha1
		/// class using the specified username and password.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		public SaslScramSha1(string username, string password) {
			username.ThrowIfNull("username");
			if (username == String.Empty)
				throw new ArgumentException("The username must not be empty.");
			password.ThrowIfNull("password");

			Username = username;
			Password = password;
		}

		/// <summary>
		/// Computes the client response to the specified SCRAM-SHA-1 challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server</param>
		/// <returns>The response to the SCRAM-SHA-1 challenge.</returns>
		/// <exception cref="SaslException">Thrown if the response could not
		/// be computed.</exception>
		protected override byte[] ComputeResponse(byte[] challenge) {
			// Precondition: Ensure username and password are not null and
			// username is not empty.
			if (String.IsNullOrEmpty(Username) || Password == null) {
				throw new SaslException("The username must not be null or empty and " +
					"the password must not be null.");
			}
			if (Step == 2)
				Completed = true;
			byte[] ret = Step == 0 ? ComputeInitialResponse() :
				(Step == 1 ? ComputeFinalResponse(challenge) :
				VerifyServerSignature(challenge));
			Step = Step + 1;
			return ret;
		}

		/// <summary>
		/// Computes the initial response sent by the client to the server.
		/// </summary>
		/// <returns>An array of bytes containing the initial client
		/// response.</returns>
		private byte[] ComputeInitialResponse() {
			// We don't support channel binding.
			return Encoding.UTF8.GetBytes("n,,n=" + SaslPrep(Username) + ",r=" +
				Cnonce);
		}

		/// <summary>
		/// Computes the "client-final-message" which completes the authentication
		/// process.
		/// </summary>
		/// <param name="challenge">The "server-first-message" challenge received
		/// from the server in response to the initial client response.</param>
		/// <returns>An array of bytes containing the client's challenge
		/// response.</returns>
		private byte[] ComputeFinalResponse(byte[] challenge) {
			NameValueCollection nv = ParseServerFirstMessage(challenge);
			// Extract the server data needed to calculate the client proof.
			string salt = nv["s"], nonce = nv["r"];
			int iterationCount = Int32.Parse(nv["i"]);
			if (!VerifyServerNonce(nonce))
				throw new SaslException("Invalid server nonce: " + nonce);
			// Calculate the client proof (refer to RFC 5802, p.7).
			string clientFirstBare = "n=" + SaslPrep(Username) + ",r=" + Cnonce,
				serverFirstMessage = Encoding.UTF8.GetString(challenge),
				withoutProof = "c=" +
				Convert.ToBase64String(Encoding.UTF8.GetBytes("n,,")) + ",r=" +
				nonce;
			AuthMessage = clientFirstBare + "," + serverFirstMessage + "," +
				withoutProof;
			SaltedPassword = Hi(Password, salt, iterationCount);
			byte[] clientKey = HMAC(SaltedPassword, "Client Key"),
				storedKey = H(clientKey),
				clientSignature = HMAC(storedKey, AuthMessage),
				clientProof = Xor(clientKey, clientSignature);
			// Return the client final message.
			return Encoding.UTF8.GetBytes(withoutProof + ",p=" +
				Convert.ToBase64String(clientProof));
		}

		/// <summary>
		/// Verifies the nonce value sent by the server.
		/// </summary>
		/// <param name="nonce">The nonce value sent by the server as part of the
		/// server-first-message.</param>
		/// <returns>True if the nonce value is valid, otherwise false.</returns>
		bool VerifyServerNonce(string nonce) {
			// The first part of the server nonce must be the nonce sent by the
			// client in its initial response.
			return nonce.StartsWith(Cnonce);
		}

		/// <summary>
		/// Verifies the server signature which is sent by the server as the final
		/// step of the authentication process.
		/// </summary>
		/// <param name="challenge">The server signature as a base64-encoded
		/// string.</param>
		/// <returns>The client's response to the server. This will be an empty
		/// byte array if verification was successful, or the '*' SASL cancellation
		/// token.</returns>
		private byte[] VerifyServerSignature(byte[] challenge) {
			string s = Encoding.UTF8.GetString(challenge);
			// The server must respond with a "v=signature" message.
			if (!s.StartsWith("v=")) {
				// Cancel authentication process.
				return Encoding.UTF8.GetBytes("*");
			}
			byte[] serverSignature = Convert.FromBase64String(s.Substring(2));
			// Verify server's signature.
			byte[] serverKey = HMAC(SaltedPassword, "Server Key"),
				calculatedSignature = HMAC(serverKey, AuthMessage);
			// If both signatures are equal, server has been authenticated. Otherwise
			// cancel the authentication process.
			return serverSignature.SequenceEqual(calculatedSignature) ?
				new byte[0] : Encoding.UTF8.GetBytes("*");
		}

		/// <summary>
		/// Parses the "server-first-message" received from the server.
		/// </summary>
		/// <param name="challenge">The challenge received from the server.</param>
		/// <returns>A collection of key/value pairs contained extracted from
		/// the server message.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the message parameter
		/// is null.</exception>
		private NameValueCollection ParseServerFirstMessage(byte[] challenge) {
			challenge.ThrowIfNull("challenge");
			string message = Encoding.UTF8.GetString(challenge);
			NameValueCollection coll = new NameValueCollection();
			foreach (string s in message.Split(',')) {
				int delimiter = s.IndexOf('=');
				if (delimiter < 0)
					continue;
				string name = s.Substring(0, delimiter), value =
					s.Substring(delimiter + 1);
				coll.Add(name, value);
			}
			return coll;
		}

		/// <summary>
		/// Computes the "Hi()"-formula which is part of the client's response
		/// to the server challenge.
		/// </summary>
		/// <param name="password">The supplied password to use.</param>
		/// <param name="salt">The salt received from the server.</param>
		/// <param name="count">The iteration count.</param>
		/// <returns>An array of bytes containing the result of the computation of the
		/// "Hi()"-formula.</returns>
		/// <remarks>
		/// Hi is, essentially, PBKDF2 with HMAC as the pseudorandom function (PRF) and with
		/// dkLen == output length of HMAC() == output length of H(). (Refer to RFC 5802, p.6)
		/// </remarks>
		private byte[] Hi(string password, string salt, int count) {
			// The salt is sent by the server as a base64-encoded string.
			byte[] saltBytes = Convert.FromBase64String(salt);
			// Annoyingly, Rfc2898DeriveBytes only implements IDisposable in .NET 4 and upwards.
			var db = new Rfc2898DeriveBytes(password, saltBytes, count);
			try {
				// Generate 20 key bytes, which is the size of the hash result of SHA-1.
				return db.GetBytes(20);
			} finally {
#if !NET35
				if(db != null)
					db.Dispose();
#endif
			}
		}

		/// <summary>
		/// Applies the HMAC keyed hash algorithm using the specified key to
		/// the specified input data.
		/// </summary>
		/// <param name="key">The key to use for initializing the HMAC
		/// provider.</param>
		/// <param name="data">The input to compute the hashcode for.</param>
		/// <returns>The hashcode of the specified data input.</returns>
		private byte[] HMAC(byte[] key, byte[] data) {
			using (var hmac = new HMACSHA1(key)) {
				return hmac.ComputeHash(data);
			}
		}

		/// <summary>
		/// Applies the HMAC keyed hash algorithm using the specified key to
		/// the specified input string.
		/// </summary>
		/// <param name="key">The key to use for initializing the HMAC
		/// provider.</param>
		/// <param name="data">The input string to compute the hashcode for.</param>
		/// <returns>The hashcode of the specified string.</returns>
		private byte[] HMAC(byte[] key, string data) {
			return HMAC(key, Encoding.UTF8.GetBytes(data));
		}

		/// <summary>
		/// Applies the cryptographic hash function SHA-1 to the specified data
		/// array.
		/// </summary>
		/// <param name="data">The data array to apply the hash function to.</param>
		/// <returns>The hash value for the specified byte array.</returns>
		private byte[] H(byte[] data) {
			using (var sha1 = new SHA1Managed()) {
				return sha1.ComputeHash(data);
			}
		}

		/// <summary>
		/// Applies the exclusive-or operation to combine the specified byte array
		/// a with the specified byte array b.
		/// </summary>
		/// <param name="a">The first byte array.</param>
		/// <param name="b">The second byte array.</param>
		/// <returns>An array of bytes of the same length as the input arrays
		/// containing the result of the exclusive-or operation.</returns>
		/// <exception cref="ArgumentNullException">Thrown if either argument is
		/// null.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the input arrays
		/// are not of the same length.</exception>
		private byte[] Xor(byte[] a, byte[] b) {
			a.ThrowIfNull("a");
			b.ThrowIfNull("b");
			if (a.Length != b.Length)
				throw new InvalidOperationException();
			byte[] ret = new byte[a.Length];
			for (int i = 0; i < a.Length; i++) {
				ret[i] = (byte)(a[i] ^ b[i]);
			}
			return ret;
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
		/// Prepares the specified string as is described in RFC 5802.
		/// </summary>
		/// <param name="s">A string value.</param>
		/// <returns>A "Saslprepped" string.</returns>
		private static string SaslPrep(string s) {
			// Fixme: Do this properly?
			return s
				.Replace("=", "=3D")
				.Replace(",", "=2C");
		}
	}
}
