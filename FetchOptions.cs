using System;

namespace S22.Imap {
	/// <summary>
	/// Defines the different means by which mail messages may be fetched from the server.
	/// </summary>
	public enum FetchOptions {
		/// <summary>
		/// Fetches the entire mail message with all of its content.
		/// </summary>
		Normal,
		/// <summary>
		/// Only the mail message headers will be retrieved, while the actual content will not be
		/// downloaded. If this option is specified, only the header fields of the returned MailMessage
		/// object will be initialized.
		/// </summary>
		HeadersOnly,
		/// <summary>
		/// Retrieves the mail message, but will only download content that has a content-type of text.
		/// This will retrieve text as well as HTML representation, while skipping inline content and
		/// attachments.
		/// </summary>
		TextOnly,
		/// <summary>
		/// Retrieves the mail message, but skips any content that is an attachment.
		/// </summary>
		NoAttachments
	}
}
