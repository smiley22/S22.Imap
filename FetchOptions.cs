using System;

namespace S22.Imap {
	/// <summary>
	/// Fetch options that can be used with the GetMessage and GetMessages methods
	/// to selectively retrieve parts of a mail message while skipping others.
	/// </summary>
	public enum FetchOptions {
		/// <summary>
		/// Fetches the entire mail message with all of its content.
		/// </summary>
		Normal,
		/// <summary>
		/// Only the mail message headers will be retrieved, while the actual content will
		/// not be downloaded. If this option is specified, only the header fields of the
		/// returned MailMessage object will be initialized.
		/// </summary>
		HeadersOnly,
		/// <summary>
		/// Retrieves the mail message, but will only download content that has a
		/// content-type of text. This will retrieve text as well as html representations,
		/// but no inline content or attachments.
		/// </summary>
		TextOnly,
		/// <summary>
		/// Retrieves the mail message, but skips any content that has been marked as
		/// attachment.
		/// </summary>
		NoAttachments
	}
}
