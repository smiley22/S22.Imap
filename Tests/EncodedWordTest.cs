using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for decoding MIME "encoded-words".
	/// </summary>
	[TestClass]
	public class EncodedWordTest {
		/// <summary>
		/// Tests for decoding Q-encoded "encoded-word" strings.
		/// </summary>
		[TestMethod]
		public void DecodeQEncodedWord() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "Hello World", "Hello World" },
				{ "Feil på PowerShot A70", 
					"=?ISO-8859-1?Q?Feil_p=E5_PowerShot_A70?=" },
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Key, Util.DecodeWord(pair.Value));
		}

		/// <summary>
		/// Tests for decoding Base64 "encoded-word" strings.
		/// </summary>
		[TestMethod]
		public void DecodeBase64EncodedWord() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{ "", "" },
				{ "重要郵件", "=?UTF-8?B?6YeN6KaB6YO15Lu2?=" },
				{ "送信済みメール", "=?shift-jis?B?kZeQTY3Pgt2DgYFbg4s=?=" },
				{ "테스트 샘플입니다.", "=?euc-kr?B?xde9usauILv5x8PA1LTPtNku?="}
			};
			foreach (KeyValuePair<string, string> pair in dict)
				Assert.AreEqual<string>(pair.Key, Util.DecodeWord(pair.Value));
		}
	}
}
