using Microsoft.VisualStudio.TestTools.UnitTesting;
using S22.Imap.Auth.Sasl;
using S22.Imap.Auth.Sasl.Mechanisms;
using System.Text;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for the SASL XOAUTH2 authentication mechanism.
	/// </summary>
	[TestClass]
	public class OAuth2Test {
		/// <summary>
		/// Verifies the various parts of a sample authentication exchange
		/// directly taken from Google's "XOAUTH2 Mechanism" document
		/// ("Initial Client Response").
		/// </summary>
		[TestMethod]
		[TestCategory("OAuth2")]
		public void VerifyAuthenticationExchange() {
			SaslMechanism m = new SaslOAuth2("someuser@example.com",
				"vF9dft4qmTc2Nvb3RlckBhdHRhdmlzdGEuY29tCg==");
			string expectedResponse = "user=someuser@example.com\u0001" +
				"auth=Bearer vF9dft4qmTc2Nvb3RlckBhdHRhdmlzdGEuY29tCg==\u0001\u0001";
			string initialResponse = Encoding.ASCII.GetString(
				m.GetResponse(new byte[0]));
			Assert.AreEqual<string>(expectedResponse, initialResponse);
		}
	}
}
