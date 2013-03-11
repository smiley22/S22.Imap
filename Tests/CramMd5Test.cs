using Microsoft.VisualStudio.TestTools.UnitTesting;
using S22.Imap.Auth.Sasl;
using S22.Imap.Auth.Sasl.Mechanisms;
using System.Text;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for the SASL CRAM-MD5 authentication mechanism.
	/// </summary>
	[TestClass]
	public class CramMd5Test {
		/// <summary>
		/// Verifies the various parts of a sample authentication exchange
		/// directly taken from RFC 2195 ("A.1.1. Example 1", p. 6).
		/// </summary>
		[TestMethod]
		[TestCategory("Cram-Md5")]
		public void VerifyAuthenticationExchange() {
			SaslMechanism m = new SaslCramMd5("joe", "tanstaaftanstaaf");

			string initialServer = "<1896.697170952@postoffice.example.net>",
				expectedResponse = "joe 3dbc88f0624776a737b39093f6eb6427";

			string initialResponse = Encoding.ASCII.GetString(
				m.GetResponse(Encoding.ASCII.GetBytes(initialServer)));
			Assert.AreEqual<string>(expectedResponse, initialResponse);
		}
	}
}
