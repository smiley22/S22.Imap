using System;

namespace S22.Imap {
	/// <summary>
	/// Defines possible attributes for mail messages on an IMAP server.
	/// </summary>
	public enum MessageFlag {
		/// <summary>
		/// Indicates that the message has been read.
		/// </summary>
		Seen,
		/// <summary>
		/// Indicates that the message has been answered.
		/// </summary>
		Answered,
		/// <summary>
		/// Indicates that the message is "flagged" for urgent/special attention.
		/// </summary>
		Flagged,
		/// <summary>
		/// Indicates that the message has been marked as "deleted" and will be removed upon the next
		/// call to the Expunge method.
		/// </summary>
		Deleted,
		/// <summary>
		/// Indicates that the message has not completed composition and is marked as a draft.
		/// </summary>
		Draft,
		/// <summary>
		/// Indicates that the message has recently arrived in the mailbox.
		/// </summary>
		Recent
	}
}
