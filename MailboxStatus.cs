using System;

namespace S22.Imap {
	/// <summary>
	/// Represents the status information of a mailbox which can be constructed from the server
	/// response to a STATUS command.
	/// </summary>
	[Serializable]
	internal class MailboxStatus {
		/// <summary>
		/// Initializes a new MailboxStatus instance with the specified number of total and unread
		/// messages.
		/// </summary>
		/// <param name="Messages">The total number of messages in the mailbox.</param>
		/// <param name="Unread">The number of unread (unseen) messages in the mailbox.</param>
		/// <param name="NextUID">The next unique identifier value of the mailbox</param>
		internal MailboxStatus(int Messages, int Unread, uint NextUID) {
			this.Messages = Messages;
			this.Unread = Unread;
			this.NextUID = NextUID;
		}

		/// <summary>
		/// The next unique identifier value of the mailbox.
		/// </summary>
		internal uint NextUID {
			get;
			private set;
		}

		/// <summary>
		/// The total number of messages in the mailbox.
		/// </summary>
		internal int Messages {
			get;
			private set;
		}

		/// <summary>
		/// The number of unread (unseen) messages in the mailbox.
		/// </summary>
		internal int Unread {
			get;
			private set;
		}
	}
}
