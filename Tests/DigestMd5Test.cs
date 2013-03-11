using Microsoft.VisualStudio.TestTools.UnitTesting;
using S22.Imap.Auth.Sasl;
using S22.Imap.Auth.Sasl.Mechanisms;
using System;
using System.Text;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for the SASL DIGEST-MD5 authentication mechanism.
	/// </summary>
	[TestClass]
	public class DigestMd5Test {
		/// <summary>
		/// Verifies the various parts of a sample authentication exchange
		/// directly taken from RFC 2831 ("4 Example", p. 17-18).
		/// </summary>
		[TestMethod]
		[TestCategory("Digest-Md5")]
		public void VerifyAuthenticationExchange() {
			SaslMechanism m = new SaslDigestMd5("chris", "secret", "OA6MHXh6VqTrRk");
			string initialServer = "realm=\"elwood.innosoft.com\",nonce=\"OA6MG9" +
				"tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8",
				expectedResponse = "username=\"chris\",realm=\"elwood.innosoft.com\"," +
				"nonce=\"OA6MG9tEQGm2hh\",nc=00000001,cnonce=\"OA6MHXh6VqTrRk\"," +
				"digest-uri=\"imap/elwood.innosoft.com\"," +
				"response=d388dad90d4bbd760a152321f2143af7,qop=auth";
			
			string initialResponse = Encoding.ASCII.GetString(
				m.GetResponse(Encoding.ASCII.GetBytes(initialServer)));
			Assert.AreEqual<string>(expectedResponse, initialResponse);
			string finalResponse = Encoding.ASCII.GetString(
				m.GetResponse(Encoding.ASCII.GetBytes("rspauth=ea40f60335c427b5" +
				"527b84dbabcdfffd")));
			Assert.AreEqual<string>(String.Empty, finalResponse);
		}
	}
}
