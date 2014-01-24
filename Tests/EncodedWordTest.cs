using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;

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
				{ "=?ISO-8859-1?Q?Feil_p=E5_PowerShot_A70?=",
					"Feil på PowerShot A70" },
				{ "=?utf-8?Q?=E5=84=AA:_2013__NEW_PROUDCTS__RD_LED_T?=\r\n" +
					"\t=?utf-8?Q?V=2C_TABLET_PC_=2C_PORTABLE?=\r\n",
					"優: 2013  NEW PROUDCTS  RD LED TV, TABLET PC , PORTABLE" },
				{ "Information =?ISO-8859-1?Q?f=F6r?= dig",
					"Information för dig" },
				{ "faktura =?ISO-8859-1?Q?F14072-=F6stersund=2Epdf?=",
					"faktura F14072-östersund.pdf" },
				{ "=?US-ASCII*EN?Q?Keith_Moore?=", "Keith Moore" }
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Value, Util.DecodeWords(pair.Key));
		}

		/// <summary>
		/// Tests for decoding Base64 "encoded-words" strings.
		/// </summary>
		[TestMethod]
		public void DecodeBase64EncodedWords() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "=?UTF-8?B?6YeN6KaB6YO15Lu2?=", "重要郵件" },
				{ "=?shift-jis?B?kZeQTY3Pgt2DgYFbg4s=?=", "送信済みメール" },
				{ "=?euc-kr?B?xde9usauILv5x8PA1LTPtNku?=", "테스트 샘플입니다." },
				{ "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?=", "cmndy.kang(亢鲷鑫)" },
				{ "=?utf-8?B?QmV0YWxuaW5nc2F2aSBhdnNlZW5kZSAuc2UtZG9tw6RubmFt?=\r\n" +
					"\t=?utf-8?B?bg==?=",
					"Betalningsavi avseende .se-domännamn" },
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Value, Util.DecodeWords(pair.Key));
		}

		/// <summary>
		/// Tests for decoding AddressLists "encoded-words" strings.
		/// </summary>
		[TestMethod]
		[TestCategory("ParseAddressList")]
		public void DecodeEncodedAddressLists() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?= <cindy.kang@xxxcorp.com>",
					"\"cmndy.kang(亢鲷鑫)\" <cindy.kang@xxxcorp.com>" },
				{ "undisclosed recipients: ;", "" },
				{ "undisclosed-recipients:;", "" },
				{ "\"Hiroyuki Tanaka, Japan\" <MLAXXX_XX.Mu-lti+sub@s_u-b.nifty.com>",
					"\"Hiroyuki Tanaka, Japan\" <MLAXXX_XX.Mu-lti+sub@s_u-b.nifty.com>" },
				{ "test1 <test1@domain>, \"test2\" <test2@domain>, \"test, nr3\" <test3@domain>",
					"\"test1\" <test1@domain>, \"test2\" <test2@domain>, \"test, nr3\" <test3@domain>" },
				// This is not supported by the MailAddress class, but valid RFC.
				{ "only.local", "" },
				{ "@;", "" },
				// Minimum length of a mail address is at least 3 characters.
				{ "<@>", "" },
				{ "<a@b>", "a@b" },
				// This should be decoded to opqkuv@m.linaaken.com@yahoo.com, but not valid in .NET
				// From Issue #48.
				// This test fails when compiling for .NET 3.5, because the .NET 3.5
				// MailAddressCollection implementation differs from .NET 4.0 and upwards.
				{ "opqkuv@m.linaaken.com<=?UTF-8?B?b3Bxa3V2QG0ubGluYWFrZW4uY29t?=@yahoo.com>;",
					"\"opqkuv@m.linaaken.com\" <=?UTF-8?B?b3Bxa3V2QG0ubGluYWFrZW4uY29t?=@yahoo.com>" },
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
				} catch (System.Exception ex) {
					result.Append("exception: " + ex.ToString());
				}
				Assert.AreEqual<string>(pair.Value, result.ToString(), "Indata: " + pair.Key);
			}
		}
	}
}
