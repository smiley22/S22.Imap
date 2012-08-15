using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S22.Imap {
	/// <summary>
	/// Contains status information for a mailbox.
	/// </summary>
	public class MailboxStatus {
		/// <summary>
		/// Initializes a new MailboxStatus instance with the specified number
		/// of total and unread messages.
		/// </summary>
		/// <param name="Messages"></param>
		/// <param name="Unread"></param>
		internal MailboxStatus(int Messages, int Unread) {
			this.Messages = Messages;
			this.Unread = Unread;
		}

		/// <summary>
		/// The total number of messages in the mailbox.
		/// </summary>
		public int Messages {
			get;
			private set;
		}

		/// <summary>
		/// The number of unread (unseen) messages in the mailbox.
		/// </summary>
		public int Unread {
			get;
			private set;
		}
	}
}
