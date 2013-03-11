using Microsoft.VisualStudio.TestTools.UnitTesting;
using S22.Imap.Auth.Sasl;
using S22.Imap.Auth.Sasl.Mechanisms;
using System.Text;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for the SASL PLAIN authentication mechanism.
	/// </summary>
	[TestClass]
	public class PlainTest {
		/// <summary>
		/// Verifies the various parts of a sample authentication exchange
		/// directly taken from RFC 4616 ("4. Examples", p. 5).
		/// </summary>
		[TestMethod]
		[TestCategory("Plain authentication")]
		public void VerfiyAuthenticationExchange() {
			SaslMechanism m = new SaslPlain("tim", "tanstaaftanstaaf");

			string expectedResponse = "\0tim\0tanstaaftanstaaf";
			string initialResponse = Encoding.ASCII.GetString(
				m.GetResponse(new byte[0]));
			Assert.AreEqual<string>(expectedResponse, initialResponse);
		}
	}
}
