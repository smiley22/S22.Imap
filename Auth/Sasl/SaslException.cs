using System;
using System.Runtime.Serialization;

namespace S22.Imap.Auth.Sasl {
	/// <summary>
	/// The exception is thrown when a Sasl-related error or unexpected condition occurs.
	/// </summary>
	[Serializable()]
	internal class SaslException : Exception {
		/// <summary>
		/// Initializes a new instance of the SaslException class
		/// </summary>
		public SaslException() : base() { }
		/// <summary>
		/// Initializes a new instance of the SaslException class with its message
		/// string set to <paramref name="message"/>.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		public SaslException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the SaslException class with its message
		/// string set to <paramref name="message"/> and a reference to the inner exception that
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		/// <param name="inner">The exception that is the cause of the current exception.</param>
		public SaslException(string message, Exception inner) : base(message, inner) { }
		/// <summary>
		/// Initializes a new instance of the SaslException class with the specified
		/// serialization and context information.
		/// </summary>
		/// <param name="info">An object that holds the serialized object data about the exception
		/// being thrown. </param>
		/// <param name="context">An object that contains contextual information about the source
		/// or destination. </param>
		protected SaslException(System.Runtime.Serialization.SerializationInfo info, StreamingContext context)
			: base(info, context) { }
	}
}
