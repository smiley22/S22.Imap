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
			MailMessage m = MessageBuilder.FromHeader(Properties.Resources.MailHeader);
			Assert.AreEqual<string>(Properties.Resources.MailSubject, m.Subject);
			Assert.AreEqual<string>(Properties.Resources.MailSubjectEncoding, m.SubjectEncoding.BodyName);
			Assert.AreEqual<string>(Properties.Resources.MailFromName, m.From.DisplayName);
			Assert.AreEqual<string>(Properties.Resources.MailFromAddress, m.From.Address);
			Assert.AreEqual<MailPriority>(MailPriority.High, m.Priority);
			Assert.AreEqual<string>(Properties.Resources.MailXMimeOLE, m.Headers["X-MimeOLE"]);
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string and verifies the constructed
		/// binary attachment is valid.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithAttachment() {
			MailMessage m = MessageBuilder.FromMIME822(Properties.Resources.MailWithZipAttachment);

			Assert.AreEqual<int>(0, m.AlternateViews.Count);
			Assert.AreEqual<int>(1, m.Attachments.Count);
			// Ensure constructed attachment is identical to our resource file.
			Assert.AreEqual<string>(Properties.Resources.AttachmentName, m.Attachments[0].Name);
			using (var sr = new BinaryReader(m.Attachments[0].ContentStream)) {
				byte[] constructed = sr.ReadBytes((int) sr.BaseStream.Length);
				Assert.AreEqual<int>(
					Properties.Resources.ZipAttachment.Length,
					constructed.Length);
				Assert.IsTrue(Properties.Resources.ZipAttachment.SequenceEqual(constructed));
			}
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string containing quoted-printable
		/// encoded text.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithQuotedPrintables() {
			// Line-ending \r\n fixes making sure resource data have CrLf in case of inconsistency in
			// resource from git checkout. Noted in Issue #37.
			MailMessage m = MessageBuilder.FromMIME822(
				Properties.Resources.MailWithQuotedPrintables.
				Replace("\r\n", "\n").Replace("\n", "\r\n"));
			// Ensure the constructed body is identical to our resource string.
			Assert.AreEqual<string>(Properties.Resources.QuotedPrintableText.
				Replace("\r\n", "\n").Replace("\n", "\r\n"), m.Body);
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string containing multiple nested MIME
		/// parts.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithMultipleParts() {
			MailMessage m = MessageBuilder.FromMIME822(Properties.Resources.MailWithMultipleParts);

			// The mail message contains text as well as html and image/gif and audio/mid MIME parts.
			Assert.IsFalse(m.IsBodyHtml);
			Assert.AreEqual<int>(1, m.AlternateViews.Count);
			Assert.AreEqual<int>(2, m.Attachments.Count);

			Assert.AreEqual<string>("text/html", m.AlternateViews[0].ContentType.MediaType);
			Assert.AreEqual<string>("image/gif", m.Attachments[0].ContentType.MediaType);
			Assert.AreEqual<string>("audio/mid", m.Attachments[1].ContentType.MediaType);

			// Verify constructed image/gif and audio/mid content is identical to our resource files.
			using (var sr = new BinaryReader(m.Attachments[0].ContentStream)) {
				byte[] gif = sr.ReadBytes((int) sr.BaseStream.Length);
				Assert.AreEqual<int>(Properties.Resources.GifContent.Length, gif.Length);
				Assert.IsTrue(Properties.Resources.GifContent.SequenceEqual(gif));
			}
			using (var sr = new BinaryReader(m.Attachments[1].ContentStream)) {
				byte[] midi = sr.ReadBytes((int) sr.BaseStream.Length);
				Assert.AreEqual<int>(Properties.Resources.MidiContent.Length, midi.Length);
				Assert.IsTrue(Properties.Resources.MidiContent.SequenceEqual(midi));
			}
		}

		/// <summary>
		/// Creates a MailMessage instance from an RFC822/MIME string containing an attachment without
		/// a disposition.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithMultipartMixedAttachment() {
			// The multiparts is manipulated base64 data and not the real files.
			MailMessage m = MessageBuilder.FromMIME822(Properties.Resources.MailWithoutDisposition);

			// Check the From header.
			Assert.AreEqual("", m.From.DisplayName, "Expected no From displayname.");
			Assert.AreEqual("invxxxxx@xxxxx.com", m.From.Address, "Unexpected From address.");
			// Check the To header.
			Assert.AreEqual(1, m.To.Count, "Unexpected To address count.");
			Assert.AreEqual("", m.To[0].DisplayName, "Expected no To displayname.");
			Assert.AreEqual("faktxxx@xxxxxx.se", m.To[0].Address, "Unexpected To address.");
			Assert.AreEqual("Fakt__xxxx___xxxx___07_02_2013", m.Subject, "Unexpected Subject.");
			Assert.AreEqual(
							"multipart/mixed;\tboundary=\"--boundary_0_6cb33448-390c-4f02-b75a-2738f1d6dd45\"",
							m.Headers["Content-Type"], "Unexpected Content-Type");
			Assert.IsFalse(m.IsBodyHtml, "Expected non HTML body");
			Assert.AreEqual(Encoding.UTF8, m.BodyEncoding, "Unexpected Body Encoding.");
			// Ensure that we get the message correct.
			Assert.AreEqual(Encoding.UTF8.GetString(Util.Base64Decode(
				"U8OkbmRlciBow6RybWVkIMO2dmVyIGJpbGFnb3IgdXRza3Jpdm5hIHNlbmFzdGUgZmFrdHVyZXJp" +
				"bmdzZGFnZW4uDQoNCk1lZCB24oCebmxpZyBo4oCebHNuaW5nDQo=")),
				m.Body);
			Assert.AreEqual(0, m.AlternateViews.Count, "AlternateViews count missmatch.");
			Assert.AreEqual(1, m.Attachments.Count, "Attachment count missmatch.");
			Assert.AreEqual("Fakt_fil_xxxx_20130207.pdf", m.Attachments[0].Name,
				"Attachment name missmatch.");
		}

		/// <summary>
		/// Ensures a malformed, invalid or missing From header is handled properly. See issue #50.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithInvalidFromHeader() {
			MailMessage m = MessageBuilder.FromMIME822(Properties.Resources.MailWithInvalidFromHeader);
			Assert.IsNull(m.From);
			Assert.AreEqual<string>("broxi@lcoalmail.loc", m.To.ToString());
			Assert.AreEqual<int>(1, m.Attachments.Count);
			Assert.AreEqual<string>("winmail.dat", m.Attachments[0].Name);
			Assert.AreEqual<string>("AQIWacilbO2h+QbxaliA6L/xOzdkTQ==", m.Headers["Thread-Index"]);
		}

		/// <summary>
		/// Ensures encoded attachment names are properly decoded. See issue #28.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithEncodedAttachmentName() {
			MailMessage m = MessageBuilder.FromMIME822(
				Properties.Resources.MailWithEncodedAttachmentName);
			Assert.AreEqual<int>(1, m.Attachments.Count);
			Assert.AreEqual<string>("тест.txt", m.Attachments[0].Name);
		}

		/// <summary>
		/// Ensures RFC2231-encoded attachment names are properly decoded.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithRfc2231Headers() {
			MailMessage m = MessageBuilder.FromMIME822(Properties.Resources.MailWithRfc2231Headers);
			Assert.AreEqual<int>(4, m.Attachments.Count);
			Assert.AreEqual<string>(m.Attachments[0].Name, "hogohoge0.jpeg");
			Assert.AreEqual<string>(m.Attachments[1].Name, "hogohoge1.jpeg");
			Assert.AreEqual<string>(m.Attachments[2].Name, "ほごほげ2.jpeg");
			Assert.AreEqual<string>(m.Attachments[3].Name, "ほごほげ3.jpeg");

			m = MessageBuilder.FromMIME822(Properties.Resources.MailWithRfc2231Headers2);
			Assert.AreEqual<int>(1, m.Attachments.Count);
			Assert.AreEqual<string>(m.Attachments[0].Name,
				"0305 - MULTILINGUES - Solicitação de orçamento (Alemão Português).doc");
		}

		/// <summary>
		/// Ensures RFC822/MIME comments are processed properly.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildMessageFromMIME822")]
		public void MessageWithMIMEComments() {
			MailMessage m = MessageBuilder.FromMIME822(Properties.Resources.MailWithMIMEComments);

			Assert.AreEqual<string>("1.0", m.Headers["Mime-version"]);
			Assert.AreEqual<bool>(true, m.IsBodyHtml);
			Assert.AreEqual<string>("utf-8", m.BodyEncoding.WebName);
			Assert.AreEqual<string>("HUB02.mailcluster.uni-bonn.de",
				m.Headers["X-MS-Exchange-Organization-AuthSource"]);
			// Parentheses in the subject should be left alone.
			Assert.AreEqual<string>("Business Development Meeting (Important)", m.Subject);
			// Parentheses in double-quoted strings should also be left alone.
			Assert.AreEqual<string>("Taylor (John) Evans", m.From.DisplayName);
			Assert.AreEqual<string>("example_from@dc.edu", m.From.Address);
			Assert.AreEqual<string>("Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.0.1) " +
				"Gecko/20020823 Netscape/7.0", m.Headers["User-Agent"]);
		}

		/// <summary>
		/// Ensures address-lists with multiple addresses are properly parsed.
		/// </summary>
		[TestMethod]
		[TestCategory("ParseAddressList")]
		public void ParseAddressListWithMultipleAddresses() {
			string list = "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?= <cindy.kang@xxxcorp.com>, " +
				"\"Hiroyuki Tanaka, Japan\" <MLAXXX_XX.Mu-lti+sub@s_u-b.nifty.com>, " +
				"mark <mark@example.net>";
			MailAddress[] addr = MessageBuilder.ParseAddressList(list);
			Assert.AreEqual<int>(3, addr.Length);
			Assert.AreEqual<string>("Hiroyuki Tanaka, Japan", addr[1].DisplayName);
			Assert.AreEqual<string>("mark", addr[2].DisplayName);
		}

		/// <summary>
		/// Ensures invalid entries in an address-list are skipped while valid entries are still
		/// parsed.
		/// </summary>
		[TestMethod]
		[TestCategory("ParseAddressList")]
		public void ParseAddressListWithInvalidEntries() {
			string list = "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?= <cindy.kang@xxxcorp.com>, " +
				// Missing closing bracket.
				"\"Hiroyuki Tanaka, Japan\" <MLAXXX_XX.Mu-lti+sub@s_u-b.nifty.com, " +
				// Invalid mail address.
				"mark mark@";
			MailAddress[] addr = MessageBuilder.ParseAddressList(list);
			Assert.AreEqual<int>(1, addr.Length);
			Assert.AreEqual<string>("cmndy.kang(亢鲷鑫)", addr[0].DisplayName);
		}

		/// <summary>
		/// Ensures an empty address-list can be processed.
		/// </summary>
		[TestMethod]
		[TestCategory("ParseAddressList")]
		public void ParseEmptyAddressList() {
			string list = string.Empty;
			MailAddress[] addr = MessageBuilder.ParseAddressList(list);
			Assert.AreEqual<int>(0, addr.Length);
			addr = MessageBuilder.ParseAddressList(null);
			Assert.AreEqual<int>(0, addr.Length);
		}
	}
}
