using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;

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

			Assert.AreEqual<int>(0, m.AlternateViews.Count);
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
			// Lineending \r\n fixes making sure resource data have CrLf in case
			// of inconsistensy in resource from git checkout. Noted in Issue #37
			MailMessage m = MessageBuilder.FromMIME822(
				Properties.Resources.MailWithQuotedPrintables.
				Replace("\r\n", "\n").Replace("\n", "\r\n"));
			// Ensure the constructed body is identical to our resource string.
			Assert.AreEqual<string>(Properties.Resources.QuotedPrintableText.
				Replace("\r\n", "\n").Replace("\n", "\r\n"),
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
			// AlternateViews only if multipart/alternative ?
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

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string
		/// containing attachment without disposition.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithMultipartMixedAttachment()
		{
			// The multiparts is manipulated base64 data and not the real files.
			MailMessage m = MessageBuilder.FromMIME822(
				"Received: from xxx.xxxxx.com (xx.xxxx.com [123.45.67.89])\r\n" +
				"\tby xxxx.asxxx.se (Postfix) with ESMTP id E357F2CCC6A\r\n" +
				"\tfor <faktxxx@xxxxxx.se>; Thu,  7 Feb 2013 17:12:05 +0100 (CET)\r\n" +
				"MIME-Version: 1.0\r\n" +
				"From: <invxxxxx@xxxxx.com>\r\n" +
				"To: <faktxxx@xxxxxx.se>\r\n" +
				"Date: Thu, 7 Feb 2013 17:12:02 +0100\r\n" +
				"Subject: Fakt__xxxx___xxxx___07_02_2013\r\n" +
				"Content-Type: multipart/mixed;\r\n" +
				"\tboundary=\"--boundary_0_6cb33448-390c-4f02-b75a-2738f1d6dd45\"\r\n" +
				"Message-ID: <44967f46-57cc-47c4-ab9f-1c450e1d87bb@XXXXI0123.xx.xxxx.com>\r\n" +
				"\r\n" +
				"----boundary_0_6cb33448-390c-4f02-b75a-2738f1d6dd45\r\n" +
				"Content-Type: text/plain; charset=\"utf-8\"\r\n" +
				"Content-Transfer-Encoding: base64\r\n" +
				"\r\n" +
				"U8OkbmRlciBow6RybWVkIMO2dmVyIGJpbGFnb3IgdXRza3Jpdm5hIHNlbmFzdGUgZmFrdHVyZXJp\r\n" +
				"bmdzZGFnZW4uDQoNCk1lZCB24oCebmxpZyBo4oCebHNuaW5nDQo=\r\n" +
				"----boundary_0_6cb33448-390c-4f02-b75a-2738f1d6dd45\r\n" +
				"Content-Type: application/octet-stream; name=\"Fakt_fil_xxxx_20130207.pdf\"\r\n" +
				"Content-Transfer-Encoding: base64\r\n" +
				"\r\n" +
				"JVBERi0xLjMNCiX15Pb8DQoNCjcgMCBvYmogPDwgL1R5cGUgL1hPYmplY3QNCi9TdWJ0eXBl\r\n" +
				"IC9JbWFnZQ0KL05hbWUgL0kxDQovV2lkdGggNDYxDQovSGVpZ2h0IDExMA0KL0JpdHNQZXJD\r\n" +
				"b21wb25lbnQgOA0KL0NvbG9yU3BhY2UgL0RldmljZUdyYXkNCi9MZW5ndGggOTUyOCAvRmls\r\n" +
				"dGVyIFsgL0FTQ0lJODVEZWNvZGUgL0ZsYXRlRGVjb2RlIF0gPj4NCnN0cmVhbQ0KR2IiLyxN\r\n" +
				"NDU0MFtKcD8/V14uckZCMXQuLUBjRVxBclFMVkgzJW1iWWR0bEpcRkVdO21faT==\r\n" +
				"----boundary_0_6cb33448-390c-4f02-b75a-2738f1d6dd45--\r\n");
			// Check From header
			Assert.AreEqual("", m.From.DisplayName, "Expected no From displayname");
			Assert.AreEqual("invxxxxx@xxxxx.com", m.From.Address, "Unexpected From address");
			// Check To header
			Assert.AreEqual(1, m.To.Count, "Unexpected To address count");
			Assert.AreEqual("", m.To[0].DisplayName, "Expected no To displayname");
			Assert.AreEqual("faktxxx@xxxxxx.se", m.To[0].Address, "Unexpected To address");

			Assert.AreEqual("Fakt__xxxx___xxxx___07_02_2013", m.Subject, "Unexpected Subject");
			Assert.AreEqual(m.Headers["Content-Type"],
							"multipart/mixed;\tboundary=\"--boundary_0_6cb33448-390c-4f02-b75a-2738f1d6dd45\"",
							"Unexpected Content-Type");

			Assert.IsFalse(m.IsBodyHtml, "Expected non HTML body");
			Assert.AreEqual(Encoding.UTF8, m.BodyEncoding, "Unexpected Body Encoding");
			// Ensure that we get the message correct.
			Assert.AreEqual(Encoding.UTF8.GetString(Util.Base64Decode(
				"U8OkbmRlciBow6RybWVkIMO2dmVyIGJpbGFnb3IgdXRza3Jpdm5hIHNlbmFzdGUgZmFrdHVyZXJp" +
				"bmdzZGFnZW4uDQoNCk1lZCB24oCebmxpZyBo4oCebHNuaW5nDQo=")),
				m.Body);

			Assert.AreEqual(0, m.AlternateViews.Count, "AlternateViews count missmatch");

			Assert.AreEqual(1, m.Attachments.Count, "Attachment count missmatch");
			Assert.AreEqual("Fakt_fil_xxxx_20130207.pdf", m.Attachments[0].Name, "Attachment name missmatch");
		}
	}
}
