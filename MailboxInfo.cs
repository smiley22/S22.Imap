using System;
using System.Collections.Generic;

namespace S22.Imap {
	/// <summary>
	/// Provides access to status information such as the total number of messages and quota
	/// information for a mailbox.
	/// </summary>
	/// <remarks>
	/// The terms "mailbox" and "folder" can be used interchangeably and refer to the IMAP concept of
	/// multiple server-side directories into which messages can be stored (such as "Inbox",
	/// "Sent Items", "Trash", etc.).
	/// </remarks>
	[Serializable]
	public class MailboxInfo {
		/// <summary>
		/// Initializes a new instance of the MailboxInfo class with the specified values.
		/// </summary>
		/// <param name="Name">The IMAP name of the mailbox.</param>
		/// <param name="Flags">The IMAP flags set on this mailbox.</param>
		/// <param name="Messages">The number of messages in the mailbox.</param>
		/// <param name="Unread">The number of unread messages in the mailbox.</param>
		/// <param name="NextUID">The next unique identifier (UID) of the mailbox.</param>
		/// <param name="UsedStorage">The amount of used storage of the mailbox, in bytes.</param>
		/// <param name="FreeStorage">The amount of free storage of the mailbox, in bytes.</param>
		internal MailboxInfo(string Name, IEnumerable<MailboxFlag> Flags, int Messages, int Unread,
			uint NextUID, UInt64 UsedStorage, UInt64 FreeStorage) {
				this.Name = Name;
				this.Flags = Flags;
				this.Messages = Messages;
				this.Unread = Unread;
				this.NextUID = NextUID;
				this.UsedStorage = UsedStorage;
				this.FreeStorage = FreeStorage;
		}

		/// <summary>
		/// Returns the name of the mailbox.
		/// </summary>
		/// <returns>The name of the mailbox</returns>
		public override string ToString() {
			return Name;
		}

		/// <summary>
		/// The name of the mailbox.
		/// </summary>
		public string Name {
			get;
			private set;
		}

		/// <summary>
		/// An enumerable collection of flags set on the mailbox.
		/// </summary>
		public IEnumerable<MailboxFlag> Flags {
			get;
			private set;
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
		/// The next unique identifier value of the mailbox.
		/// </summary>
		public uint NextUID {
			get;
			private set;
		}

		/// <summary>
		/// The amount of used storage in the mailbox, measured in bytes.
		/// </summary>
		/// <remarks>Not all IMAP servers support the retrieval of quota information. If it is not
		/// possible to retrieve the amount of used storage, this property will be 0.
		/// </remarks>
		public UInt64 UsedStorage {
			get;
			private set;
		}

		/// <summary>
		/// The amount of free storage in the mailbox, measured in bytes.
		/// </summary>
		/// <remarks>Not all IMAP servers support the retrieval of quota information. If it is not
		/// possible to retrieve the amount of free storage, this property will be 0.
		/// </remarks>
		public UInt64 FreeStorage {
			get;
			private set;
		}
	}
}
