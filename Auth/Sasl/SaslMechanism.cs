using System.Collections.Generic;
using System;

namespace S22.Imap.Auth.Sasl {
	/// <summary>
	/// The abstract base class from which all classes implementing a Sasl
	/// authentication mechanism must derive.
	/// </summary>
	internal abstract class SaslMechanism {
		/// <summary>
		/// IANA name of the authentication mechanism.
		/// </summary>
		public abstract string Name {
			get;
		}

		/// <summary>
		/// True if the authentication exchange between client and server
		/// has been completed.
		/// </summary>
		public abstract bool IsCompleted {
			get;
		}

		/// <summary>
		/// A map of mechanism-specific properties which are needed by the
		/// authentication mechanism to compute it's challenge-responses.
		/// </summary>
		public Dictionary<string, object> Properties {
			get;
			private set;
		}

		/// <summary>
		/// Computes the client response to a challenge sent by the server.
		/// </summary>
		/// <param name="challenge"></param>
		/// <returns>The client response to the specified challenge.</returns>
		protected abstract byte[] ComputeResponse(byte[] challenge);


		/// <summary>
		/// </summary>
		internal SaslMechanism() {
			Properties = new Dictionary<string, object>();
		}

		/// <summary>
		/// Retrieves the base64-encoded client response for the specified
		/// base64-encoded challenge sent by the server.
		/// </summary>
		/// <param name="challenge">A base64-encoded string representing a challenge
		/// sent by the server.</param>
		/// <returns>A base64-encoded string representing the client response to the
		/// server challenge.</returns>
		/// <remarks>The IMAP, POP3 and SMTP authentication commands expect challenges
		/// and responses to be base64-encoded. This method automatically decodes the
		/// server challenge before passing it to the Sasl implementation and
		/// encodes the client response to a base64-string before returning it to the
		/// caller.</remarks>
		/// <exception cref="SaslException">Thrown if the client response could
		/// not be retrieved. Refer to the inner exception for error details.</exception>
		public string GetResponse(string challenge) {
			try {
				byte[] data = String.IsNullOrEmpty(challenge) ? new byte[0] :
					Convert.FromBase64String(challenge);
				byte[] response = ComputeResponse(data);
				return Convert.ToBase64String(response);
			} catch (Exception e) {
				throw new SaslException("The challenge-response could not be " +
					"retrieved.", e);
			}
		}

		/// <summary>
		/// Retrieves the client response for the specified server challenge.
		/// </summary>
		/// <param name="challenge">A byte array containing the challenge sent by
		/// the server.</param>
		/// <returns>An array of bytes representing the client response to the
		/// server challenge.</returns>
		public byte[] GetResponse(byte[] challenge) {
			return ComputeResponse(challenge);
		}
	}
}
