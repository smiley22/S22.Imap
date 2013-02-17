using System;
using System.Security.Cryptography;
using System.Text;

namespace S22.Imap.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Contains methods for calculating the various Type 3 challenge
	/// responses.
	/// </summary>
	internal static class Responses {
		/// <summary>
		/// Computes the LM-response to the challenge sent as part of an
		/// NTLM type 2 message.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server.</param>
		/// <param name="password">The user account password.</param>
		/// <returns>An array of bytes representing the response to the
		/// specified challenge.</returns>
		internal static byte[] ComputeLMResponse(byte[] challenge,
			string password) {
			byte[] lmHash = LMHash(password);
			return LMResponse(lmHash, challenge);
		}

		/// <summary>
		/// Computes the NTLM-response to the challenge sent as part of an
		/// NTLM type 2 message.
		/// </summary>
		/// <param name="challenge">The challenge sent by the server.</param>
		/// <param name="password">The user account password.</param>
		/// <returns>An array of bytes representing the response to the
		/// specified challenge.</returns>
		internal static byte[] ComputeNtlmResponse(byte[] challenge,
			string password) {
			byte[] ntlmHash = NtlmHash(password);
			return LMResponse(ntlmHash, challenge);
		}

		/// <summary>
		/// Creates the LM Hash of the specified password.
		/// </summary>
		/// <param name="password">The password to create the LM Hash of.</param>
		/// <returns>The LM Hash of the given password, used in the calculation
		/// of the LM Response.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the password argument
		/// is null.</exception>
		private static byte[] LMHash(string password) {
			// Precondition: password != null.
			password.ThrowIfNull("password");
			byte[] oemPassword =
				Encoding.ASCII.GetBytes(password.ToUpperInvariant()),
				magic = new byte[] { 0x4b, 0x47, 0x53, 0x21, 0x40, 0x23, 0x24, 0x25 },
				// This is the pre-encrypted magic value with a null DES key.
				nullEncMagic = { 0xAA, 0xD3, 0xB4, 0x35, 0xB5, 0x14, 0x04, 0xEE },
				keyBytes = new byte[14], lmHash = new byte[16];
			int length = Math.Min(oemPassword.Length, 14);
			Array.Copy(oemPassword, keyBytes, length);
			byte[] lowKey = CreateDESKey(keyBytes, 0), highKey =
				CreateDESKey(keyBytes, 7);

			using (DES des = DES.Create("DES")) {
				byte[] output = new byte[8];
				des.Mode = CipherMode.ECB;
				// Note: In .NET DES cannot accept a weak key. This can happen for
				// an empty password or if the password is shorter than 8 characters.
				if (password.Length < 1) {
					Buffer.BlockCopy(nullEncMagic, 0, lmHash, 0, 8);
				} else {
					des.Key = lowKey;
					using (var encryptor = des.CreateEncryptor()) {
						encryptor.TransformBlock(magic, 0, magic.Length, lmHash, 0);
					}
				}
				if (password.Length < 8) {
					Buffer.BlockCopy(nullEncMagic, 0, lmHash, 8, 8);
				} else {
					des.Key = highKey;
					using (var encryptor = des.CreateEncryptor()) {
						encryptor.TransformBlock(magic, 0, magic.Length, lmHash, 8);
					}
				}
				return lmHash;
			}
		}

		/// <summary>
		/// Creates a DES encryption key from the specified key material.
		/// </summary>
		/// <param name="bytes">The key material to create the DES encryption
		/// key from.</param>
		/// <param name="offset">An offset into the byte array at which to
		/// extract the key material from.</param>
		/// <returns>A 56-bit DES encryption key as an array of bytes.</returns>
		private static byte[] CreateDESKey(byte[] bytes, int offset) {
			byte[] keyBytes = new byte[7];
			Array.Copy(bytes, offset, keyBytes, 0, 7);
			byte[] material = new byte[8];
			material[0] = keyBytes[0];
			material[1] = (byte) (keyBytes[0] << 7 | (keyBytes[1] & 0xff) >> 1);
			material[2] = (byte) (keyBytes[1] << 6 | (keyBytes[2] & 0xff) >> 2);
			material[3] = (byte) (keyBytes[2] << 5 | (keyBytes[3] & 0xff) >> 3);
			material[4] = (byte) (keyBytes[3] << 4 | (keyBytes[4] & 0xff) >> 4);
			material[5] = (byte) (keyBytes[4] << 3 | (keyBytes[5] & 0xff) >> 5);
			material[6] = (byte) (keyBytes[5] << 2 | (keyBytes[6] & 0xff) >> 6);
			material[7] = (byte) (keyBytes[6] << 1);

			return OddParity(material);
		}

		/// <summary>
		/// Applies odd parity to the specified byte array.
		/// </summary>
		/// <param name="bytes">The byte array to apply odd parity to.</param>
		/// <returns>A reference to the byte array.</returns>
		private static byte[] OddParity(byte[] bytes) {
			for (int i = 0; i < bytes.Length; i++) {
				byte b = bytes[i];
				bool needsParity = (((b >> 7) ^ (b >> 6) ^ (b >> 5) ^
					(b >> 4) ^ (b >> 3) ^ (b >> 2) ^
					(b >> 1)) & 0x01) == 0;
				if (needsParity)
					bytes[i] |= (byte) 0x01;
				else
					bytes[i] &= (byte) 0xFE;
			}
			return bytes;
		}

		/// <summary>
		/// Creates the LM Response from the specified hash and Type 2 challenge.
		/// </summary>
		/// <param name="hash">An LM or NTLM hash.</param>
		/// <param name="challenge">The server challenge from the Type 2
		/// message.</param>
		/// <returns>The challenge response as an array of bytes.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the hash or the
		/// challenge parameter is null.</exception>
		private static byte[] LMResponse(byte[] hash, byte[] challenge) {
			hash.ThrowIfNull("hash");
			challenge.ThrowIfNull("challenge");
			byte[] keyBytes = new byte[21], lmResponse = new byte[24];
			Array.Copy(hash, 0, keyBytes, 0, 16);
			byte[] lowKey = CreateDESKey(keyBytes, 0), middleKey =
				CreateDESKey(keyBytes, 7), highKey =
				CreateDESKey(keyBytes, 14);
			using (DES des = DES.Create("DES")) {
				des.Mode = CipherMode.ECB;
				des.Key = lowKey;
				using (var encryptor = des.CreateEncryptor()) {
					encryptor.TransformBlock(challenge, 0, challenge.Length,
						lmResponse, 0);
				}
				des.Key = middleKey;
				using (var encryptor = des.CreateEncryptor()) {
					encryptor.TransformBlock(challenge, 0, challenge.Length,
						lmResponse, 8);
				}
				des.Key = highKey;
				using (var encryptor = des.CreateEncryptor()) {
					encryptor.TransformBlock(challenge, 0, challenge.Length,
						lmResponse, 16);
				}
				return lmResponse;
			}
		}

		/// <summary>
		/// Creates the NTLM Hash of the specified password.
		/// </summary>
		/// <param name="password">The password to create the NTLM hash of.</param>
		/// <returns>The NTLM hash for the specified password.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the password
		/// parameter is null.</exception>
		private static byte[] NtlmHash(String password) {
			password.ThrowIfNull("password");
			byte[] data = Encoding.Unicode.GetBytes(password);
			using (MD4 md4 = new MD4()) {
				return md4.ComputeHash(data);
			}
		}
	}
}
