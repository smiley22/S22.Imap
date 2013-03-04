using System.Net.Mail;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for decoding MIME "encoded-words".
	/// </summary>
	[TestClass]
	public class EncodedWordTest {
		/// <summary>
		/// Tests for decoding Q-encoded "encoded-words" strings.
		/// </summary>
		[TestMethod]
		public void DecodeQEncodedWords() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "Hello World", "Hello World" },
				{ "Feil på PowerShot A70",
					"=?ISO-8859-1?Q?Feil_p=E5_PowerShot_A70?=" },
				{ "優: 2013  NEW PROUDCTS  RD LED TV, TABLET PC , PORTABLE",
					"=?utf-8?Q?=E5=84=AA:_2013__NEW_PROUDCTS__RD_LED_T?=\r\n" +
					"	=?utf-8?Q?V=2C_TABLET_PC_=2C_PORTABLE?=\r\n" },
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Key, Util.DecodeWords(pair.Value));
		}

		/// <summary>
		/// Tests for decoding Base64 "encoded-words" strings.
		/// </summary>
		[TestMethod]
		public void DecodeBase64EncodedWords() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "重要郵件", "=?UTF-8?B?6YeN6KaB6YO15Lu2?=" },
				{ "送信済みメール", "=?shift-jis?B?kZeQTY3Pgt2DgYFbg4s=?=" },
				{ "테스트 샘플입니다.", "=?euc-kr?B?xde9usauILv5x8PA1LTPtNku?="},
				{ "cmndy.kang(亢鲷鑫)", "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?="},
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Key, Util.DecodeWords(pair.Value));
		}

		/// <summary>
		/// Tests for decoding AddressLists "encoded-words" strings.
		/// </summary>
		[TestMethod]
		public void DecodeEncodedAddressLists()
		{
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?= <cindy.kang@xxxcorp.com>", "\"cmndy.kang(亢鲷鑫)\" <cindy.kang@xxxcorp.com>"},
			};
			foreach (KeyValuePair<string, string> pair in dict) {
				StringBuilder result = new StringBuilder();
				try {
					MailAddress[] mails = MessageBuilder.ParseAddressList(pair.Key);
					foreach (MailAddress m in mails) {
						if (result.Length > 0)
							result.Append(", ");
						result.Append(m.ToString());
					}
				} catch {}
				Assert.AreEqual<string>(result.ToString(), pair.Value);
			}
		}
	}
}
