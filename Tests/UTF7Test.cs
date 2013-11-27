using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for encoding and decoding UTF-7 strings.
	/// </summary>
	[TestClass]
	public class UTF7Test {
		/// <summary>
		/// Tests for Util.UTF7Encode.
		/// </summary>
		[TestMethod]
		public void EncodeUTF7() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{"", ""},
				{"Hello \"World\"", "Hello \"World\""},
				{"Entwürfe", "Entw&APw-rfe"},
				{"迷惑メール", "&j,dg0TDhMPww6w-"},
				{"送信済みメール", "&kAFP4W4IMH8w4TD8MOs-"},
				{"全部郵件", "&UWiQ6JD1TvY-"},
				{"重要郵件", "&kc2JgZD1TvY-"}
			};
			foreach (KeyValuePair<string, string> p in dict)
				Assert.AreEqual<string>(p.Value, Util.UTF7Encode(p.Key));
		}

		/// <summary>
		/// Tests for Util.UTF7Decode.
		/// </summary>
		[TestMethod]
		public void DecodeUTF7() {
			Dictionary<string, string> dict = new Dictionary<string, string>() {
				{"", ""},
				{"Hello \"World\"", "Hello \"World\""},
				{"Entwürfe", "Entw&APw-rfe"},
				{"すべてのメール", "&MFkweTBmMG4w4TD8MOs-"},
				{"送信済みメール", "&kAFP4W4IMH8w4TD8MOs-"},
				{"重要郵件", "&kc2JgZD1TvY-"}
			};
			foreach (KeyValuePair<string, string> p in dict)
				Assert.AreEqual<string>(p.Key, Util.UTF7Decode(p.Value));
		}

		/// <summary>
		/// Passing invalid UTF-7 to Util.UTF7Decode should raise a FormatException.
		/// </summary>
		[TestMethod]
		[ExpectedException(typeof(FormatException))]
		public void ThrowOnInvalidUTF7() {
			string invalidUtf7 = "&Hello";
			Util.UTF7Decode(invalidUtf7);
		}
	}
}
