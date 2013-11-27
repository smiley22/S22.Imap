using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net.Mail;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for MailMessage extension methods.
	/// </summary>
	[TestClass]
	public class MailMessageTest {
		/// <summary>
		/// Basic Mailadress to and from convertion test.
		/// </summary>
		[TestMethod]
		[TestCategory("MailMessageToMIME822")]
		public void AddressToMIME822()
		{
			MailAddress from = new MailAddress("sender@foobar.com", "田中純"),
				to = new MailAddress("rctp@foobar.com", "山田太郎");
			MailAddress[] addr;

			// Test that a valid format is generated for the mailadress,
			// as well as the parsing works.
			addr = MessageBuilder.ParseAddressList(
				MailMessageExtension.To822Address(from));
			Assert.AreEqual(from.Address, addr[0].Address);
			Assert.AreEqual(from.DisplayName, addr[0].DisplayName);

			addr = MessageBuilder.ParseAddressList(
				MailMessageExtension.To822Address(to));
			Assert.AreEqual(to.Address, addr[0].Address);
			Assert.AreEqual(to.DisplayName, addr[0].DisplayName);
		}
		/// <summary>
		/// Creates an RFC822/MIME string from an existing MailMessage
		/// instance.
		/// </summary>
		[TestMethod]
		[TestCategory("MailMessageToMIME822")]
		public void MessageToMIME822() {
			MailAddress from = new MailAddress("sender@foobar.com", "田中純"),
				to = new MailAddress("rctp@foobar.com", "山田太郎");
			string cc_one = "cc-one@foobar.com", cc_two = "cc-two@foobar.com",
				subject = "大学のプレゼンテーション・秋２０１１",
				body = "第1大学は初期雇用契約導入（2006年）や大統領ニコラ・" +
				"サルコジの改革方針（2007年）に反対するバリケードストライキが行" +
				"なわれるなど、21世紀に入っても学生運動が盛ん、且つその拠点とさ" +
				"れる大学である。";
			// Create a simple mail message
			MailMessage m = new MailMessage(from, to);
			m.CC.Add(cc_one);
			m.CC.Add(cc_two);
			m.Subject = subject;
			m.Priority = MailPriority.Low;
			m.Body = body;

			string mime822 = m.ToMIME822();

			// Reconstruct MailMessage from text and verify all fields contain
			// the same values.
			MailMessage r = MessageBuilder.FromMIME822(mime822);

			Assert.AreEqual<MailAddress>(from, r.From);
			Assert.AreEqual<MailAddress>(to, r.To.First());
			Assert.AreEqual<string>(cc_one, r.CC.First().Address);
			Assert.AreEqual<string>(cc_two, r.CC.Last().Address);
			Assert.AreEqual<string>(subject, r.Subject);
			Assert.AreEqual<string>(body, r.Body);
		}

		/// <summary>
		/// Creates an RFC822/MIME string from an existing MailMessage
		/// instance containing an attachment.
		/// </summary>
		[TestMethod]
		[TestCategory("MailMessageToMIME822")]
		public void MessageWithAttachmentToMIME822() {
			MailMessage m = new MailMessage("sender@foobar.com", "rctp@foobar.com");

			m.Subject = "This is just a test.";
			m.Body = "Please take a look at the attached file.";

			// Add a zip archive as an attachment
			using (var ms = new MemoryStream(Properties.Resources.ZipAttachment)) {
				Attachment attachment = new Attachment(ms,
					new System.Net.Mime.ContentType("application/zip"));
				m.Attachments.Add(attachment);
				
				string mime822 = m.ToMIME822();
				// Reconstruct MailMessage from text and verify the constructed
				// attachment is identical to our resource file.
				MailMessage r = MessageBuilder.FromMIME822(mime822);

				Assert.AreEqual<int>(1, r.Attachments.Count);
				using (var br = new BinaryReader(r.Attachments[0].ContentStream)) {
					byte[] constructed = br.ReadBytes((int)br.BaseStream.Length);
					Assert.IsTrue(
						Properties.Resources.ZipAttachment.SequenceEqual(constructed));
				}
			}
		}
	}
}
