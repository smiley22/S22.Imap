using Microsoft.VisualStudio.TestTools.UnitTesting;
using S22.Imap.Auth.Sasl;
using S22.Imap.Auth.Sasl.Mechanisms;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for the SASL SCRAM-SHA-1 authentication mechanism.
	/// </summary>
	[TestClass]
	public class ScramSha1Test {
		/// <summary>
		/// Verifies the syntax of the client-first-message sent by the client to
		/// initiate authentication.
		/// </summary>
		[TestMethod]
		[TestCategory("Scram-Sha-1")]
		public void VerifyClientFirstMessage() {
			SaslMechanism m = new SaslScramSha1("Foo", "Bar");
			string clientInitial = Encoding.UTF8.GetString(
				m.GetResponse(new byte[0]));
			// Verify the syntax of the client-first-message.
			bool valid = Regex.IsMatch(clientInitial,
				"^[nyp],(a=[^,]+)?,(m=[^,]+,)?n=[^,]+,(r=[^,]+)(,.*)?");
			Assert.IsTrue(valid);
		}

		/// <summary>
		/// Sends the client an illegal nonce value and verifies the client
		/// subsequently raises an exception.
		/// </summary>
		[TestMethod]
		[TestCategory("Scram-Sha-1")]
		[ExpectedException(typeof(SaslException))]
		public void TamperedNonce() {
			SaslMechanism m = new SaslScramSha1("Foo", "Bar");
			// Skip the initial client response.
			m.GetResponse(new byte[0]);
			// Hand the client a server-first-message containing a nonce which is
			// missing the mandatory client-nonce part.
			byte[] serverFirst = Encoding.UTF8.GetBytes("r=123456789,s=MTIzNDU2" +
				"Nzg5,i=4096");
			// This should raise an exception.
			m.GetResponse(serverFirst);
		}

		/// <summary>
		/// Verifies the various parts of a sample authentication exchange
		/// directly taken from RFC 5802 ("SCRAM Authentication Exchange", p. 8).
		/// </summary>
		[TestMethod]
		[TestCategory("Scram-Sha-1")]
		public void VerifyAuthenticationExchange() {
			string username = "user", password = "pencil",
				cnonce = "fyko+d2lbbFgONRv9qkxdawL";
			SaslMechanism s = new SaslScramSha1(username, password, cnonce);
			string initialResponse = Encoding.UTF8.GetString(
				s.GetResponse(new byte[0]));
			// Verify the syntax of the client-first-message.
			Match m = Regex.Match(initialResponse,
				"^[nyp],(a=[^,]+)?,(m=[^,]+,)?n=([^,]+),r=([^,]+)(,.*)?");
			Assert.IsTrue(m.Success);
			Assert.AreEqual<string>(username, m.Groups[3].ToString());
			Assert.AreEqual<string>(cnonce, m.Groups[4].ToString());
			// Hand the client the server-first-message.
			byte[] serverFirst = Encoding.UTF8.GetBytes(
				"r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92," +
				"i=4096");
			string clientFinal = Encoding.UTF8.GetString(
				s.GetResponse(serverFirst));
			string expectedClientFinal = "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfc" +
				"NHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
			Assert.AreEqual<string>(expectedClientFinal, clientFinal);
			// Hand the client the server-last-message.
			byte[] serverLast = Encoding.UTF8.GetBytes("v=rmF9pqV8S7suAoZWja4dJ" +
				"RkFsKQ=");
			clientFinal = Encoding.UTF8.GetString(s.GetResponse(serverLast));
			Assert.AreEqual<string>(String.Empty, clientFinal);
		}

		/// <summary>
		/// Helper method for conveniently converting the specified string to
		/// Base64 using a decoding of UTF-8.
		/// </summary>
		/// <param name="s">The string to base64-encode.</param>
		/// <returns>A base64-encoded string.</returns>
		string ToBase64(string s) {
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
		}

		/// <summary>
		/// Helper method for conveniently decoding the specified base64-encoded
		/// string using a decoding of UTF-8.
		/// </summary>
		/// <param name="s">The base64-encoded string to decode.</param>
		/// <returns>A string constructed from the base64-decoded sequence
		/// of bytes.</returns>
		string FromBase64(string s) {
			return Encoding.UTF8.GetString(Convert.FromBase64String(s));
		}
	}
}
