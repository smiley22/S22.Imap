using System;

namespace S22.Imap {
	/// <summary>
	/// Provides data for IMAP idle notification events.
	/// </summary>
	public class IdleMessageEventArgs : EventArgs {
		/// <summary>
		/// Initializes a new instance of the IdleMessageEventArgs class and sets the
		/// MessageCount attribute to the value of the <paramref name="MessageCount"/>
		/// parameter.
		/// </summary>
		/// <param name="MessageCount">The number of messages in the selected mailbox.</param>
		/// <param name="MessageUID"> The unique identifier (UID) of the newest message in the
		/// mailbox.</param>
		/// <param name="Client">The instance of the ImapClient class that raised the event.</param>
		internal IdleMessageEventArgs(uint MessageCount, uint MessageUID,
			ImapClient Client) {
			this.MessageCount = MessageCount;
			this.MessageUID = MessageUID;
			this.Client = Client;
		}

		/// <summary>
		/// The total number of messages in the selected mailbox.
		/// </summary>
		public uint MessageCount {
			get;
			private set;
		}

		/// <summary>
		/// The unique identifier (UID) of the newest message in the mailbox. 
		/// </summary>
		/// <remarks>The UID can be passed to the GetMessage method in order to retrieve the mail
		/// message from the server.</remarks>
		public uint MessageUID {
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
