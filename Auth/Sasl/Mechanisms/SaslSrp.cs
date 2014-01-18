using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using S22.Imap.Auth.Sasl.Mechanisms.Srp;
using System.Security.Cryptography;

namespace S22.Imap.Auth.Sasl.Mechanisms {
	/// <summary>
	/// Implements the Sasl Secure Remote Password (SRP) authentication
	/// mechanism as is described in the IETF SRP 08 draft.
	/// </summary>
	/// <remarks>
	/// This requires .NET Framework 4 because it makes use of the System.Numeric namespace
	/// which has only been part of .NET since version 4.
	/// 
	/// Some notes:
	///  - Don't bother with the example given in the IETF 08 draft
	///   document (7.5 Example); It is broken.
	///  - Integrity and confidentiality protection is not implemented.
	///   In fact, the "mandatory"-option is not supported at all.
	/// </remarks>
	internal class SaslSrp : SaslMechanism {
		bool Completed = false;

		/// <summary>
		/// SRP involves several steps.
		/// </summary>
		int Step = 0;

		/// <summary>
		/// The negotiated hash algorithm which will be used to perform any
		/// message digest calculations.
		/// </summary>
		HashAlgorithm HashAlgorithm;

		/// <summary>
		/// The public key computed as part of the authentication exchange.
		/// </summary>
		Mpi PublicKey;
		
		/// <summary>
		/// The client's private key used for calculating the client evidence.
		/// </summary>
		Mpi PrivateKey = Helper.GenerateClientPrivateKey();
		
		/// <summary>
		/// The secret key shared between client and server.
		/// </summary>
		Mpi SharedKey;

		/// <summary>
		/// The client evidence calculated as part of the authentication exchange.
		/// </summary>
		byte[] ClientProof;

		/// <summary>
		/// The options chosen by the client, picked from the list of options
		/// advertised by the server.
		/// </summary>
		string Options;

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
		/// The IANA name for the SRP authentication mechanism.
		/// </summary>
		public override string Name {
			get {
				return "SRP";
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
		/// The authorization id (userid in draft jargon).
		/// </summary>
		string AuthId {
			get {
				return Properties.ContainsKey("AuthId") ?
					Properties["AuthId"] as string : Username;
			}
			set {
				Properties["AuthId"] = value;
			}
		}

		/// <summary>
		/// Private constructor for use with Sasl.SaslFactory.
		/// </summary>
		private SaslSrp() {
			// Nothing to do here.
		}

		/// <summary>
		/// Internal constructor used for unit testing.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <param name="privateKey">The client private key to use.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		internal SaslSrp(string username, string password, byte[] privateKey)
			: this(username, password) {
				PrivateKey = new Mpi(privateKey);
		}

		/// <summary>
		/// Creates and initializes a new instance of the SaslSrp class using
		/// the specified username and password.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The plaintext password to authenticate
		/// with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username
		/// or the password parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the username
		/// parameter is empty.</exception>
		public SaslSrp(string username, string password) {
			username.ThrowIfNull("username");
			if (username == String.Empty)
				throw new ArgumentException("The username must not be empty.");
			password.ThrowIfNull("password");

			Username = username;
			Password = password;
		}

		/// <summary>
		/// Computes the client response to the specified SRP challenge.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server</param>
		/// <returns>The response to the SRP challenge.</returns>
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
			return new ClientMessage1(Username, AuthId).Serialize();
		}

		/// <summary>
		/// Computes the client response containing the client's public key and
		/// evidence.
		/// </summary>
		/// <param name="challenge">The challenge containing the protocol elements
		/// received from the server in response to the initial client
		/// response.</param>
		/// <returns>An array of bytes containing the client's challenge
		/// response.</returns>
		/// <exception cref="SaslException">Thrown if the server specified any
		/// mandatory options which are not supported.</exception>
		private byte[] ComputeFinalResponse(byte[] challenge) {
			ServerMessage1 m = ServerMessage1.Deserialize(challenge);
			// We don't support integrity protection or confidentiality.
			if (!String.IsNullOrEmpty(m.Options["mandatory"]))
				throw new SaslException("Mandatory options are not supported.");
			// Set up the message digest algorithm.
			var mda = SelectHashAlgorithm(m.Options["mda"]);
			HashAlgorithm = Activator.CreateInstance(mda.Item2) as HashAlgorithm;

			// Compute public and private key.
			PublicKey = Helper.ComputeClientPublicKey(m.Generator,
				m.SafePrimeModulus, PrivateKey);
			// Compute the shared key and client evidence.
			SharedKey = Helper.ComputeSharedKey(m.Salt, Username, Password,
				PublicKey, m.PublicKey, PrivateKey, m.Generator, m.SafePrimeModulus,
				HashAlgorithm);
			ClientProof = Helper.ComputeClientProof(m.SafePrimeModulus,
				m.Generator, Username, m.Salt, PublicKey, m.PublicKey, SharedKey,
				AuthId, m.RawOptions, HashAlgorithm);

			ClientMessage2 response = new ClientMessage2(PublicKey, ClientProof);
			// Let the server know which hash algorithm we are using.
			response.Options["mda"] = mda.Item1;
			// Remember the raw options string because we'll need it again
			// when verifying the server signature.
			Options = response.BuildOptionsString();

			return response.Serialize();
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
			ServerMessage2 m = ServerMessage2.Deserialize(challenge);
			// Compute the proof and compare it with the one sent by the server.
			byte[] proof = Helper.ComputeServerProof(PublicKey, ClientProof, SharedKey,
				AuthId, Options, m.SessionId, m.Ttl, HashAlgorithm);
			return proof.SequenceEqual(m.Proof) ? new byte[0] :
				Encoding.UTF8.GetBytes("*");
		}

		/// <summary>
		/// Selects a message digest algorithm from the specified list of
		/// supported algorithms.
		/// </summary>
		/// <returns>A tuple containing the name of the selected message digest
		/// algorithm as well as the type.</returns>
		/// <exception cref="NotSupportedException">Thrown if none of the algorithms
		/// specified in the list parameter is supported.</exception>
		private Tuple<string, Type> SelectHashAlgorithm(string list) {
			string[] supported = list.Split(',');
			var l = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
				{ "SHA-1", typeof(SHA1Managed) },
				{ "SHA-256", typeof(SHA256Managed) },
				{ "SHA-384", typeof(SHA384Managed) },
				{ "SHA-512", typeof(SHA512Managed) },
				{ "RIPEMD-160", typeof(RIPEMD160Managed) },
				{ "MD5", typeof(MD5CryptoServiceProvider) }
			};
			foreach (KeyValuePair<string, Type> p in l) {
				if (supported.Contains(p.Key, StringComparer.InvariantCultureIgnoreCase))
					return new Tuple<string, Type>(p.Key, p.Value);
			}
			throw new NotSupportedException();
		}
	}
}
