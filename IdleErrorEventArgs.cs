using System;

namespace S22.Imap {
	/// <summary>
	/// Provides data for IMAP idle error events.
	/// </summary>
	public class IdleErrorEventArgs : EventArgs {
		/// <summary>
		/// Initializes a new instance of the IdleErrorEventArgs class.
		/// </summary>
		/// <param name="exception">The exception that causes the event.</param>
		/// <param name="client">The instance of the ImapClient class that raised the event.</param>
		/// <exception cref="ArgumentNullException">The exception parameter or the client parameter
		/// is null.</exception>
		internal IdleErrorEventArgs(Exception exception, ImapClient client) {
			exception.ThrowIfNull("exception");
			client.ThrowIfNull("client");
			Exception = exception;
			Client = client;
		}

		/// <summary>
		/// The exception that caused the error event.
		/// </summary>
		public Exception Exception {
			get;
			private set;
		}

		/// <summary>
		/// The instance of the ImapClient class that raised the event.
		/// </summary>
		public ImapClient Client {
			get;
			private set;
		}
	}
}
