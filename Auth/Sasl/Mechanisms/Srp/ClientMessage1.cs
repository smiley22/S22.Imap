using S22.Imap.Auth;
using System;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents the initial client-response sent to the server to initiate
	/// the authentication exchange.
	/// </summary>
	internal class ClientMessage1 {
		/// <summary>
		/// The username to authenticate with.
		/// </summary>
		/// <remarks>SRP specification imposes a limit of 65535 bytes
		/// on this field.</remarks>
		public string Username {
			get;
			set;
		}

		/// <summary>
		/// The authorization identity to authenticate with.
		/// </summary>
		/// <remarks>SRP specification imposes a limit of 65535 bytes
		/// on this field.</remarks>
		public string AuthId {
			get;
			set;
		}

		/// <summary>
		/// The session identifier of a previous session whose parameters the
		/// client wishes to re-use.
		/// </summary>
		/// <remarks>SRP specification imposes a limit of 65535 bytes
		/// on this field. If the client wishes to initialize a new session,
		/// this parameter must be set to the empty string.</remarks> 
		public string SessionId {
			get;
			set;
		}

		/// <summary>
		/// The client's nonce used in deriving a new shared context key from
		/// the shared context key of the previous session.
		/// </summary>
		/// <remarks>SRP specification imposes a limit of 255 bytes on this
		/// field. If not needed, it must be set to an empty byte array.</remarks>
		public byte[] ClientNonce {
			get;
			set;
		}

		/// <summary>
		/// Creates a new instance of the ClientMessage1 class using the specified
		/// username.
		/// </summary>
		/// <param name="username">The username to authenticate with.</param>
		/// <param name="authId">The authorization id to authenticate with.</param>
		/// <exception cref="ArgumentNullException">Thrown if the username parameter
		/// is null.</exception>
		public ClientMessage1(string username, string authId = null) {
			username.ThrowIfNull("username");
			Username = username;
			AuthId = authId ?? String.Empty;
			SessionId = String.Empty;
			ClientNonce = new byte[0];
		}

		/// <summary>
		/// Serializes this instance of the ClientMessage1 class into a sequence of
		/// bytes according to the requirements of the SRP specification.
		/// </summary>
		/// <returns>A sequence of bytes representing this instance of the
		/// ClientMessage1 class.</returns>
		/// <exception cref="OverflowException">Thrown if the cummultative length
		/// of the serialized data fields exceeds the maximum number of bytes
		/// allowed as per SRP specification.</exception>
		/// <remarks>SRP specification imposes a limit of 2,147,483,643 bytes on
		/// the serialized data.</remarks> 
		public byte[] Serialize() {
			byte[] username = new Utf8String(Username).Serialize(),
				authId = new Utf8String(AuthId).Serialize(),
				sessionId = new Utf8String(SessionId).Serialize(),
				nonce = new OctetSequence(ClientNonce).Serialize();
			int length = username.Length +
				authId.Length + sessionId.Length + nonce.Length;
			return new ByteBuilder()
				.Append(length, true)
				.Append(username)
				.Append(authId)
				.Append(sessionId)
				.Append(nonce)
				.ToArray();
		}
	}
}
