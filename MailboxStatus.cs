using System;

namespace S22.Imap {
	/// <summary>
	/// Contains status information for a mailbox.
	/// </summary>
	[Serializable]
	public class MailboxStatus {
		/// <summary>
		/// Initializes a new MailboxStatus instance with the specified number
		/// of total and unread messages.
		/// </summary>
		/// <param name="Messages">The total number of messages in the mailbox.</param>
		/// <param name="Unread">The number of unread (unseen) messages in the mailbox.</param>
		/// <param name="Usage">The amount of occupied space in the mailbox, in bytes.</param>
		/// <param name="Free">The amount of free space in the mailbox, in bytes.</param>
		internal MailboxStatus(int Messages, int Unread, UInt64 Usage, UInt64 Free) {
			this.Messages = Messages;
			this.Unread = Unread;
			this.UsedStorage = Usage;
			this.FreeStorage = Free;
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

		/// <summary>
		/// The amount of used storage in the mailbox, measured in bytes.
		/// </summary>
		public UInt64 UsedStorage {
			get;
			private set;
		}

		/// <summary>
		/// The amount of free storage in the mailbox, measured in bytes.
		/// </summary>
		public UInt64 FreeStorage {
			get;
			private set;
		}
	}
}
