﻿using System.Net.Mail;
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
				{ "=?ISO-8859-1?Q?Feil_p=E5_PowerShot_A70?=",
					"Feil på PowerShot A70" },
				{ "Information =?ISO-8859-1?Q?f=F6r?= dig",
					"Information för dig" },
				{ "=?utf-8?Q?=E5=84=AA:_2013__NEW_PROUDCTS__RD_LED_T?=\r\n" +
					"\t=?utf-8?Q?V=2C_TABLET_PC_=2C_PORTABLE?=\r\n",
					"優: 2013  NEW PROUDCTS  RD LED TV, TABLET PC , PORTABLE" },
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
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Value, Util.DecodeWords(pair.Key));
		}

		/// <summary>
		/// Tests for decoding AddressLists "encoded-words" strings.
		/// </summary>
		[TestMethod]
		public void DecodeEncodedAddressLists()
		{
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "=?gb2312?B?Y21uZHkua2FuZyi/uvb09s4p?= <cindy.kang@xxxcorp.com>",
					"\"cmndy.kang(亢鲷鑫)\" <cindy.kang@xxxcorp.com>" },
				{ "undisclosed recipients: ;", "" }, /* want display: "undisclosed recipients: ;" */
				{ "undisclosed-recipients:;", "" }, /* want display: "undisclosed-recipients:;" */
				{ "\"Hiroyuki Tanaka, Japan\" <MLAXXX_XX.Mu-lti+sub@s_u-b.nifty.com>",
					"\"Hiroyuki Tanaka, Japan\" <MLAXXX_XX.Mu-lti+sub@s_u-b.nifty.com>" },
				{ "test1 <test1@domain>, \"test2\" <test2@domain>, \"test, nr3\" <test3@domain>",
					"\"test1\" <test1@domain>, \"test2\" <test2@domain>, \"test, nr3\" <test3@domain>" },
				{ "only.local", "" }, /* this is not supported by the MailAddress Class, but should be. */
				{ "@;", "" },
				{ "<@>", "" },
				{ "<a@b>", "a@b" },
				/* this should be decoded? Real adress is then: opqkuv@m.linaaken.com@yahoo.com ? */
				{ "opqkuv@m.linaaken.com<=?UTF-8?B?b3Bxa3V2QG0ubGluYWFrZW4uY29t?=@yahoo.com>;",
					"\"opqkuv@m.linaaken.com\" <=?UTF-8?B?b3Bxa3V2QG0ubGluYWFrZW4uY29t?=@yahoo.com>" }, // Issue #48 
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
