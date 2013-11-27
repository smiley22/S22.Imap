using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for decoding quoted-printables.
	/// </summary>
	[TestClass]
	public class QuotedPrintableTest {
		/// <summary>
		/// Tests for Util.QPDecode.
		/// </summary>
		[TestMethod]
		public void DecodeQuotedPrintable() {
			var dict = new Dictionary<KeyValuePair<string, Encoding>, string>() {
				{
					new KeyValuePair<string, Encoding>("=91=e5=8a=77=82=cc=83=76=83" +
						"=8c=83=5b=83=93=83=65=81=5b=83=56=83=87=83=93=81=45=8f=48=82" +
						"=51=82=4f=82=50=82=50",
						Encoding.GetEncoding("shift-jis")),
						"大学のプレゼンテーション・秋２０１１"
				},
				{
					new KeyValuePair<string, Encoding>("Auftragsbest=E4tigung Ihrer " +
					"Wei=DFwein Bestellung",
					Encoding.GetEncoding("Windows-1252")),
					"Auftragsbestätigung Ihrer Weißwein Bestellung"
				},
				{
					new KeyValuePair<string, Encoding>("Another test with =\r\nsoft " +
						"line breaks: a^2 + b^2 =3D c^2", Encoding.ASCII),
						"Another test with soft line breaks: a^2 + b^2 = c^2"
				}
			};
			foreach (KeyValuePair<KeyValuePair<string, Encoding>, string> p in dict)
				Assert.AreEqual<string>(p.Value, Util.QPDecode(p.Key.Key, p.Key.Value));
		}
	}
}
