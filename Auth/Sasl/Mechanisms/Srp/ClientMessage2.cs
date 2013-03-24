using S22.Imap.Auth;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents the second client-response sent to the server as part of
	/// the SRP authentication exchange.
	/// </summary>
	internal class ClientMessage2 {
		/// <summary>
		/// The client's ephemeral public key.
		/// </summary>
		public Mpi PublicKey {
			get;
			set;
		}

		/// <summary>
		/// The evidence which proves to the server client-knowledge of the shared
		/// context key.
		/// </summary>
		public byte[] Proof {
			get;
			set;
		}

		/// <summary>
		/// The options list indicating the security services chosen by the client.
		/// </summary>
		public NameValueCollection Options {
			get;
			private set;
		}

		/// <summary>
		/// The initial vector the server will use to set up its encryption
		/// context, if confidentiality protection will be employed.
		/// </summary>
		public byte[] InitialVector {
			get;
			set;
		}

		/// <summary>
		/// Creates and initializes a new instance of the ClientMessage2 class.
		/// </summary>
		private ClientMessage2() {
			Options = new NameValueCollection();
			InitialVector = new byte[0];
		}

		/// <summary>
		/// Creates and initializes a new instance of the ClientMessage2 class using
		/// the specified public key and client proof.
		/// </summary>
		/// <param name="publicKey">The client's public key.</param>
		/// <param name="proof">The calculated client proof.</param>
		/// <exception cref="ArgumentNullException">Thrown if either the public key
		/// or the proof parameter is null.</exception>
		public ClientMessage2(Mpi publicKey, byte[] proof)
			: this() {
			publicKey.ThrowIfNull("publicKey");
			proof.ThrowIfNull("proof");

			PublicKey = publicKey;
			Proof = proof;
		}

		/// <summary>
		/// Serializes this instance of the ClientMessage2 class into a sequence of
		/// bytes according to the requirements of the SRP specification.
		/// </summary>
		/// <returns>A sequence of bytes representing this instance of the
		/// ClientMessage2 class.</returns>
		/// <exception cref="OverflowException">Thrown if the cummultative length
		/// of the serialized data fields exceeds the maximum number of bytes
		/// allowed as per SRP specification.</exception>
		/// <remarks>SRP specification imposes a limit of 2,147,483,643 bytes on
		/// the serialized data.</remarks> 
		public byte[] Serialize() {
			byte[] publicKey = PublicKey.Serialize(),
				M1 = new OctetSequence(Proof).Serialize(),
				cIV = new OctetSequence(InitialVector).Serialize(),
				options = new Utf8String(BuildOptionsString()).Serialize();
			int length = publicKey.Length + M1.Length + cIV.Length +
				options.Length;
			return new ByteBuilder()
				.Append(length, true)
				.Append(publicKey)
				.Append(M1)
				.Append(options)
				.Append(cIV)
				.ToArray();
		}

		/// <summary>
		/// Serializes the client's options collection into a comma-seperated
		/// options string.
		/// </summary>
		/// <returns>A comma-seperated string containing the client's chosen
		/// options.</returns>
		public string BuildOptionsString() {
			List<string> list = new List<string>();
			foreach (string key in Options) {
				if (String.IsNullOrEmpty(Options[key]) || "true".Equals(
					Options[key], StringComparison.InvariantCultureIgnoreCase))
					list.Add(key);
				else
					list.Add(key + "=" + Options[key]);
			}
			return String.Join(",", list.ToArray());
		}
	}
}
