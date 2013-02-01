using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net.Mail;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for the MessageBuilder class.
	/// </summary>
	[TestClass]
	public class MessageBuilderTest {
		/// <summary>
		/// Tests for MessageBuilder.FromHeader.
		/// </summary>
		[TestMethod]
		public void BuildMessageFromHeader() {
			MailMessage m = MessageBuilder.FromHeader(
				Properties.Resources.MailHeader);
			Assert.AreEqual<string>(Properties.Resources.MailSubject, m.Subject);
			Assert.AreEqual<string>(Properties.Resources.MailSubjectEncoding,
				m.SubjectEncoding.BodyName);
			Assert.AreEqual<string>(Properties.Resources.MailFromName,
				m.From.DisplayName);
			Assert.AreEqual<string>(Properties.Resources.MailFromAddress,
				m.From.Address);
			Assert.AreEqual<MailPriority>(MailPriority.High, m.Priority);
			Assert.AreEqual<string>(Properties.Resources.MailXMimeOLE,
				m.Headers["X-MimeOLE"]);
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string
		/// and verifies the constructed binary attachment is valid.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithAttachment() {
			MailMessage m = MessageBuilder.FromMIME822(
				Properties.Resources.MailWithZipAttachment);

			Assert.AreEqual<int>(1, m.Attachments.Count);
			// Ensure constructed attachment is identical to our resource file.
			Assert.AreEqual<string>(Properties.Resources.AttachmentName,
				m.Attachments[0].Name);
			using (var sr = new BinaryReader(m.Attachments[0].ContentStream)) {
				byte[] constructed = sr.ReadBytes((int) sr.BaseStream.Length);

				Assert.AreEqual<int>(
					Properties.Resources.ZipAttachment.Length,
					constructed.Length);
				Assert.IsTrue(Properties.Resources.ZipAttachment
					.SequenceEqual(constructed));
			}
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string
		/// containing quoted-printable-encoded text.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithQuotedPrintables() {
			MailMessage m = MessageBuilder.FromMIME822(
				Properties.Resources.MailWithQuotedPrintables);
			// Ensure the constructed body is identical to our resource string.
			Assert.AreEqual<string>(Properties.Resources.QuotedPrintableText,
				m.Body);
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string
		/// containing multiple nested MIME parts.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithMultipleParts() {
			MailMessage m = MessageBuilder.FromMIME822(
				Properties.Resources.MailWithMultipleParts);

			// The mail message contains text as well as html and
			// image/gif and audio/mid MIME parts.
			Assert.IsFalse(m.IsBodyHtml);
			Assert.AreEqual<int>(3, m.AlternateViews.Count);

			Assert.AreEqual<string>("text/html",
				m.AlternateViews[0].ContentType.MediaType);
			Assert.AreEqual<string>("image/gif",
				m.AlternateViews[1].ContentType.MediaType);
			Assert.AreEqual<string>("audio/mid",
				m.AlternateViews[2].ContentType.MediaType);

			// Verify constructed image/gif and audio/mid content is identical to
			// our resource files.
			using (var sr = new BinaryReader(m.AlternateViews[1].ContentStream)) {
				byte[] gif = sr.ReadBytes((int) sr.BaseStream.Length);
				Assert.AreEqual<int>(
					Properties.Resources.GifContent.Length,
					gif.Length);
				Assert.IsTrue(Properties.Resources.GifContent
					.SequenceEqual(gif));
			}

			using (var sr = new BinaryReader(m.AlternateViews[2].ContentStream)) {
				byte[] midi = sr.ReadBytes((int) sr.BaseStream.Length);
				Assert.AreEqual<int>(
					Properties.Resources.MidiContent.Length,
					midi.Length);
				Assert.IsTrue(Properties.Resources.MidiContent
					.SequenceEqual(midi));
			}
		}
	}
}
