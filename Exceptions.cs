using System;
using System.Runtime.Serialization;

namespace S22.Imap {
	/// <summary>
	/// The exception that is thrown when an unexpected response is received from the server.
	/// </summary>
	[Serializable]
	public class BadServerResponseException : Exception {
		/// <summary>
		/// Initializes a new instance of the BadServerResponseException class
		/// </summary>
		public BadServerResponseException() : base() { }
		/// <summary>
		/// Initializes a new instance of the BadServerResponseException class with its message
		/// string set to <paramref name="message"/>.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		public BadServerResponseException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the BadServerResponseException class with its message
		/// string set to <paramref name="message"/> and a reference to the inner exception that
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		/// <param name="inner">The exception that is the cause of the current exception.</param>
		public BadServerResponseException(string message, Exception inner) : base(message, inner) { }
		/// <summary>
		/// Initializes a new instance of the BadServerResponseException class with the specified
		/// serialization and context information.
		/// </summary>
		/// <param name="info">An object that holds the serialized object data about the exception
		/// being thrown. </param>
		/// <param name="context">An object that contains contextual information about the source
		/// or destination. </param>
		protected BadServerResponseException(SerializationInfo info, StreamingContext context)
			: base(info, context) { }
	}

	/// <summary>
	/// The exception that is thrown when the supplied credentials were rejected by the server.
	/// </summary>
	[Serializable]
	public class InvalidCredentialsException : Exception {
		/// <summary>
		/// Initializes a new instance of the InvalidCredentialsException class
		/// </summary>
		public InvalidCredentialsException() : base() { }
		/// <summary>
		/// Initializes a new instance of the InvalidCredentialsException class with its message
		/// string set to <paramref name="message"/>.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		public InvalidCredentialsException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the InvalidCredentialsException class with its message
		/// string set to <paramref name="message"/> and a reference to the inner exception that
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		/// <param name="inner">The exception that is the cause of the current exception.</param>
		public InvalidCredentialsException(string message, Exception inner) : base(message, inner) { }
		/// <summary>
		/// Initializes a new instance of the InvalidCredentialsException class with the specified
		/// serialization and context information.
		/// </summary>
		/// <param name="info">An object that holds the serialized object data about the exception
		/// being thrown. </param>
		/// <param name="context">An object that contains contextual information about the source
		/// or destination. </param>
		protected InvalidCredentialsException(SerializationInfo info, StreamingContext context)
			: base(info, context) { }
	}

	/// <summary>
	/// The exception that is thrown when a client has not authenticated with the server and
	/// attempts to call a method which can only be called when authenticated.
	/// </summary>
	[Serializable]
	public class NotAuthenticatedException : Exception {
		/// <summary>
		/// Initializes a new instance of the NotAuthenticatedException class
		/// </summary>
		public NotAuthenticatedException() : base() { }
		/// <summary>
		/// Initializes a new instance of the NotAuthenticatedException class with its message
		/// string set to <paramref name="message"/>.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		public NotAuthenticatedException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the NotAuthenticatedException class with its message
		/// string set to <paramref name="message"/> and a reference to the inner exception that
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		/// <param name="inner">The exception that is the cause of the current exception.</param>
		public NotAuthenticatedException(string message, Exception inner) : base(message, inner) { }
		/// <summary>
		/// Initializes a new instance of the NotAuthenticatedException class with the specified
		/// serialization and context information.
		/// </summary>
		/// <param name="info">An object that holds the serialized object data about the exception
		/// being thrown. </param>
		/// <param name="context">An object that contains contextual information about the source
		/// or destination. </param>
		protected NotAuthenticatedException(SerializationInfo info, StreamingContext context)
			: base(info, context) { }
	}
}
