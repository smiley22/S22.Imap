using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Contains helper methods for calculating the various components of the
	/// SRP authentication exchange.
	/// </summary>
	internal static class Helper {
		/// <summary>
		/// The trace source used for informational and debug messages.
		/// </summary>
		static TraceSource ts = new TraceSource("S22.Imap.Sasl.Srp");

		/// <summary>
		/// Determines whether the specified modulus is valid.
		/// </summary>
		/// <param name="N">The modulus to validate.</param>
		/// <returns>True if the specified modulus is valid, otherwise
		/// false.</returns>
		public static bool IsValidModulus(Mpi N) {
			foreach (string s in moduli) {
				BigInteger a = BigInteger.Parse(s, NumberStyles.HexNumber);
				if (BigInteger.Compare(a, N.Value) == 0)
					return true;
			}
			// Fixme: Perform proper validation?
			return false;
		}

		/// <summary>
		/// Determines whether the specified generator is valid.
		/// </summary>
		/// <param name="g">The generator to validate.</param>
		/// <returns>True if the specified generator is valid, otherwise
		/// false.</returns>
		public static bool IsValidGenerator(Mpi g) {
			return BigInteger.Compare(new BigInteger(2), g.Value) == 0;
		}

		/// <summary>
		/// Generates a random "multi-precision integer" which will act as the
		/// client's private key.
		/// </summary>
		/// <returns>The client's ephemeral private key as a "multi-precision
		/// integer".</returns>
		public static Mpi GenerateClientPrivateKey() {
			using (var rng = new RNGCryptoServiceProvider()) {
				byte[] data = new byte[16];
				rng.GetBytes(data);

				return new Mpi(data);
			}
		}

		/// <summary>
		/// Calculates the client's ephemeral public key.
		/// </summary>
		/// <param name="generator">The generator sent by the server.</param>
		/// <param name="safePrimeModulus">The safe prime modulus sent by
		/// the server.</param>
		/// <param name="privateKey">The client's private key.</param>
		/// <returns>The client's ephemeral public key as a
		/// "multi-precision integer".</returns>
		/// <remarks>
		/// A = Client Public Key
		/// g = Generator
		/// a = Client Private Key
		/// N = Safe Prime Modulus
		/// </remarks>
		public static Mpi ComputeClientPublicKey(Mpi generator, Mpi safePrimeModulus,
			Mpi privateKey) {
			// A = g ^ a % N
			BigInteger result = BigInteger.ModPow(generator.Value, privateKey.Value,
				safePrimeModulus.Value);

			return new Mpi(result);
		}

		/// <summary>
		/// Calculates the shared context key K from the given parameters.
		/// </summary>
		/// <param name="salt">The user's password salt.</param>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="password">The password to authenticate with.</param>
		/// <param name="clientPublicKey">The client's ephemeral public key.</param>
		/// <param name="serverPublicKey">The server's ephemeral public key.</param>
		/// <param name="clientPrivateKey">The client's private key.</param>
		/// <param name="generator">The generator sent by the server.</param>
		/// <param name="safePrimeModulus">The safe prime modulus sent by the
		/// server.</param>
		/// <param name="hashAlgorithm">The negotiated hash algorithm to use
		/// for the calculations.</param>
		/// <returns>The shared context key K as a "multi-precision
		/// integer".</returns>
		/// <remarks>
		/// A = Client Public Key
		/// B = Server Public Key
		/// N = Safe Prime Modulus
		/// U = Username
		/// p = Password
		/// s = User's Password Salt
		/// a = Client Private Key
		/// g = Generator
		/// K = Shared Public Key
		/// </remarks>
		public static Mpi ComputeSharedKey(byte[] salt, string username,
			string password, Mpi clientPublicKey, Mpi serverPublicKey,
			Mpi clientPrivateKey, Mpi generator, Mpi safePrimeModulus,
			HashAlgorithm hashAlgorithm) {
			// u = H(A | B)
			byte[] u = hashAlgorithm.ComputeHash(new ByteBuilder()
				.Append(clientPublicKey.ToBytes())
				.Append(serverPublicKey.ToBytes())
				.ToArray());
			// x = H(s | H(U | ":" | p))
			byte[] up = hashAlgorithm.ComputeHash(
				Encoding.UTF8.GetBytes(username + ":" + password)),
				sup = new ByteBuilder().Append(salt).Append(up).ToArray(),
				x = hashAlgorithm.ComputeHash(sup);
			// S = ((B - (3 * g ^ x)) ^ (a + u * x)) % N
			Mpi _u = new Mpi(u), _x = new Mpi(x);
			ts.TraceInformation("ComputeSharedKey: _u = " + _u.Value.ToString("X"));
			ts.TraceInformation("ComputeSharedKey: _x = " + _x.Value.ToString("X"));
			// base = B - (3 * (g ^ x))
			
			BigInteger _base = BigInteger.Subtract(serverPublicKey.Value,
				BigInteger.Multiply(new BigInteger(3),
				BigInteger.ModPow(generator.Value, _x.Value, safePrimeModulus.Value)) %
				safePrimeModulus.Value);
			if (_base.Sign < 0)
				_base = BigInteger.Add(_base, safePrimeModulus.Value);
			ts.TraceInformation("ComputeSharedKey: _base = " + _base.ToString("X"));

			// Alternative way to calculate base; This is not being used in actual calculations
			// but still here to ease debugging.
			BigInteger gx = BigInteger.ModPow(generator.Value, _x.Value, safePrimeModulus.Value),
				gx3 = BigInteger.Multiply(new BigInteger(3), gx) % safePrimeModulus.Value;
			ts.TraceInformation("ComputeSharedKey: gx = " + gx.ToString("X"));
			BigInteger @base = BigInteger.Subtract(serverPublicKey.Value, gx3) % safePrimeModulus.Value;
			if (@base.Sign < 0)
				@base = BigInteger.Add(@base, safePrimeModulus.Value);
			ts.TraceInformation("ComputeSharedKey: @base = " + @base.ToString("X"));

			// exp = a + u * x
			BigInteger exp = BigInteger.Add(clientPrivateKey.Value,
				BigInteger.Multiply(_u.Value, _x.Value)),
			S = BigInteger.ModPow(_base, exp, safePrimeModulus.Value);
			ts.TraceInformation("ComputeSharedKey: exp = " + exp.ToString("X"));
			ts.TraceInformation("ComputeSharedKey: S = " + S.ToString("X"));

			// K = H(S)
			return new Mpi(hashAlgorithm.ComputeHash(new Mpi(S).ToBytes()));
		}

		/// <summary>
		/// Computes the client evidence from the given parameters.
		/// </summary>
		/// <param name="safePrimeModulus">The safe prime modulus sent by the
		/// server.</param>
		/// <param name="generator">The generator sent by the server.</param>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="salt">The client's password salt.</param>
		/// <param name="clientPublicKey">The client's ephemeral public key.</param>
		/// <param name="serverPublicKey">The server's ephemeral public key.</param>
		/// <param name="sharedKey">The shared context key.</param>
		/// <param name="authId">The authorization identity.</param>
		/// <param name="options">The raw options string as received from the
		/// server.</param>
		/// <param name="hashAlgorithm">The message digest algorithm to use for
		/// calculating the client proof.</param>
		/// <returns>The client proof as an array of bytes.</returns>
		public static byte[] ComputeClientProof(Mpi safePrimeModulus, Mpi generator,
			string username, byte[] salt, Mpi clientPublicKey, Mpi serverPublicKey,
			Mpi sharedKey, string authId, string options, HashAlgorithm hashAlgorithm) {
			byte[] N = safePrimeModulus.ToBytes(), g = generator.ToBytes(),
				U = Encoding.UTF8.GetBytes(username), s = salt,
				A = clientPublicKey.ToBytes(), B = serverPublicKey.ToBytes(),
				K = sharedKey.ToBytes(), I = Encoding.UTF8.GetBytes(authId),
				L = Encoding.UTF8.GetBytes(options);
			HashAlgorithm H = hashAlgorithm;
			// The proof is calculated as follows:
			//
			// H( bytes(H( bytes(N) )) ^ bytes( H( bytes(g) ))
			//  | bytes(H( bytes(U) ))
			//  | bytes(s)
			//  | bytes(A)
			//  | bytes(B)
			//  | bytes(K)
			//  | bytes(H( bytes(I) ))
			//  | bytes(H( bytes(L) ))
			// )
			byte[] seq = new ByteBuilder()
				.Append(Xor(H.ComputeHash(N), H.ComputeHash(g)))
				.Append(H.ComputeHash(U))
				.Append(s)
				.Append(A)
				.Append(B)
				.Append(K)
				.Append(H.ComputeHash(I))
				.Append(H.ComputeHash(L))
				.ToArray();
			return H.ComputeHash(seq);
		}

		/// <summary>
		/// Computes the server evidence from the given parameters.
		/// </summary>
		/// <param name="clientPublicKey">The client's ephemeral public key.</param>
		/// <param name="clientProof"></param>
		/// <param name="sharedKey">The shared context key.</param>
		/// <param name="authId">The authorization identity.</param>
		/// <param name="options">The raw options string as sent by the
		/// client.</param>
		/// <param name="sid">The session id sent by the server.</param>
		/// <param name="ttl">The time-to-live value for the session id sent
		/// by the server.</param>
		/// <param name="hashAlgorithm">The message digest algorithm to use for
		/// calculating the server proof.</param>
		/// <returns>The server proof as an array of bytes.</returns>
		public static byte[] ComputeServerProof(Mpi clientPublicKey, byte[] clientProof,
			Mpi sharedKey, string authId, string options, string sid, uint ttl,
			HashAlgorithm hashAlgorithm) {
			byte[] A = clientPublicKey.ToBytes(), M1 = clientProof,
				K = sharedKey.ToBytes(), I = Encoding.UTF8.GetBytes(authId),
				o = Encoding.UTF8.GetBytes(options), _sid = Encoding.UTF8.GetBytes(sid);
			HashAlgorithm H = hashAlgorithm;
			// The proof is calculated as follows:
			//
			// H( bytes(A)
			//  | bytes(M1)
			//  | bytes(K)
			//  | bytes(H( bytes(I) ))
			//  | bytes(H( bytes(o) ))
			//  | bytes(sid)
			//  | ttl
			// )
			byte[] seq = new ByteBuilder()
				.Append(A)
				.Append(M1)
				.Append(K)
				.Append(H.ComputeHash(I))
				.Append(H.ComputeHash(o))
				.Append(_sid)
				.Append(ttl, true)
				.ToArray();
			return H.ComputeHash(seq);
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
		static byte[] Xor(byte[] a, byte[] b) {
			a.ThrowIfNull("a");
			b.ThrowIfNull("b");
			if (a.Length != b.Length)
				throw new InvalidOperationException();
			byte[] ret = new byte[a.Length];
			for (int i = 0; i < a.Length; i++) {
				ret[i] = (byte) (a[i] ^ b[i]);
			}
			return ret;
		}

		#region Recommended Modulus values
		/// <summary>
		/// Recommended values for the safe prime modulus (Refer to Appendix A.
		/// "Modulus and Generator Values" of the IETF SRP draft).
		/// </summary>
		static string[] moduli = new string[] {
			"115B8B692E0E045692CF280B436735C77A5A9E8A9E7ED56C965F87DB5B2A2ECE3",
			"8025363296FB943FCE54BE717E0E2958A02A9672EF561953B2BAA3BAACC3ED5754" +
			"EB764C7AB7184578C57D5949CCB41B",
			"D4C7F8A2B32C11B8FBA9581EC4BA4F1B04215642EF7355E37C0FC0443EF756EA2C" +
			"6B8EEB755A1C723027663CAA265EF785B8FF6A9B35227A52D86633DBDFCA43",
			"C94D67EB5B1A2346E8AB422FC6A0EDAEDA8C7F894C9EEEC42F9ED250FD7F0046E5" +
			"AF2CF73D6B2FA26BB08033DA4DE322E144E7A8E9B12A0E4637F6371F34A2071C4B" +
			"3836CBEEAB15034460FAA7ADF483",
			"B344C7C4F8C495031BB4E04FF8F84EE95008163940B9558276744D91F7CC9F4026" +
			"53BE7147F00F576B93754BCDDF71B636F2099E6FFF90E79575F3D0DE694AFF737D" +
			"9BE9713CEF8D837ADA6380B1093E94B6A529A8C6C2BE33E0867C60C3262B",
			"EEAF0AB9ADB38DD69C33F80AFA8FC5E86072618775FF3C0B9EA2314C9C256576D6" +
			"74DF7496EA81D3383B4813D692C6E0E0D5D8E250B98BE48E495C1D6089DAD15DC7" +
			"D7B46154D6B6CE8EF4AD69B15D4982559B297BCF1885C529F566660E57EC68EDBC" +
			"3C05726CC02FD4CBF4976EAA9AFD5138FE8376435B9FC6",
			"D77946826E811914B39401D56A0A7843A8E7575D738C672A090AB1187D690DC438" +
			"72FC06A7B6A43F3B95BEAEC7DF04B9D242EBDC481111283216CE816E004B786C5F" +
			"CE856780D41837D95AD787A50BBE90BD3A9C98AC0F5FC0DE744B1CDE1891690894" +
			"BC1F65E00DE15B4B2AA6D87100C9ECC2527E45EB849DEB14BB2049B163EA04187F" +
			"D27C1BD9C7958CD40CE7067A9C024F9B7C5A0B4F5003686161F0605B",
			"9DEF3CAFB939277AB1F12A8617A47BBBDBA51DF499AC4C80BEEEA9614B19CC4D5F" +
			"4F5F556E27CBDE51C6A94BE4607A291558903BA0D0F84380B655BB9A22E8DCDF02" +
			"8A7CEC67F0D08134B1C8B97989149B609E0BE3BAB63D47548381DBC5B1FC764E3F" +
			"4B53DD9DA1158BFD3E2B9C8CF56EDF019539349627DB2FD53D24B7C48665772E43" +
			"7D6C7F8CE442734AF7CCB7AE837C264AE3A9BEB87F8A2FE9B8B5292E5A021FFF5E" +
			"91479E8CE7A28C2442C6F315180F93499A234DCF76E3FED135F9BB",
			"AC6BDB41324A9A9BF166DE5E1389582FAF72B6651987EE07FC3192943DB56050A3" +
			"7329CBB4A099ED8193E0757767A13DD52312AB4B03310DCD7F48A9DA04FD50E808" +
			"3969EDB767B0CF6095179A163AB3661A05FBD5FAAAE82918A9962F0B93B855F979" +
			"93EC975EEAA80D740ADBF4FF747359D041D5C33EA71D281E446B14773BCA97B43A" +
			"23FB801676BD207A436C6481F1D2B9078717461A5B9D32E688F87748544523B524" +
			"B0D57D5EA77A2775D2ECFA032CFBDBF52FB3786160279004E57AE6AF874E7303CE" +
			"53299CCC041C7BC308D82A5698F3A8D0C38271AE35F8E9DBFBB694B5C803D89F7A" +
			"E435DE236D525F54759B65E372FCD68EF20FA7111F9E4AFF73"
		};
		#endregion
	}
}
