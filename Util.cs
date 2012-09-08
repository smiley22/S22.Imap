using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap {
	/// <summary>
	/// A static utility class containing methods for decoding encoded
	/// non-ASCII data as is often used in mail messages as well as
	/// extension methods for some existing classes.
	/// </summary>
	internal static class Util {
		/// <summary>
		/// Returns a copy of the string enclosed in double-quotes and with escaped
		/// CRLF, back-slash and double-quote characters (as is expected by some
		/// commands of the IMAP protocol).
		/// </summary>
		/// <param name="value">Extends the System.String class</param>
		/// <returns>A copy of the string enclosed in double-quotes and properly
		/// escaped as is required by the IMAP protocol.</returns>
		internal static string QuoteString(this string value) {
			return "\"" + value
				.Replace("\\", "\\\\")
				.Replace("\r", "\\r")
				.Replace("\n", "\\n")
				.Replace("\"", "\\\"") + "\"";
		}

		/// <summary>
		/// Returns true if the string contains only ASCII characters.
		/// </summary>
		/// <param name="s">Extension method for the String class.</param>
		/// <returns>Returns true if the string contains only ASCII characters,
		/// otherwise false is returned.</returns>
		internal static bool IsASCII(this string s) {
			return s.ToCharArray().All(c => c < 127);
		}

		/// <summary>
		/// Splits a string into chunks of the specified number of
		/// characters.
		/// </summary>
		/// <param name="str">Extension method for the String class.</param>
		/// <param name="characters">The length of a chunk, measured in
		/// characters.</param>
		/// <returns>An array of string chunks</returns>
		internal static string[] ToChunks(this string str, int characters) {
			List<string> list = new List<string>();
			while (str.Length > 0) {
				int length = str.Length > characters ? characters :
					str.Length;
				string t = str.Substring(0, length);
				str = str.Remove(0, length);
				list.Add(t);
			}
			return list.ToArray();
		}

		/// <summary>
		/// Raises the event. Ensures the event is only raised, if it is not null.
		/// </summary>
		/// <typeparam name="T">Extends System.EventHandler class"/></typeparam>
		/// <param name="event">Extends System.EventHandler class</param>
		/// <param name="sender">The sender of the event</param>
		/// <param name="args">The event arguments associated with this event</param>
		internal static void Raise<T>(this EventHandler<T> @event, object sender, T args)
			where T : EventArgs {
			if (@event == null)
				return;
			@event(sender, args);
		}

		/// <summary>
		/// Decodes a string composed of one or several MIME 'encoded-words'.
		/// </summary>
		/// <param name="words">A string to composed of one or several MIME
		/// 'encoded-words'</param>
		/// <exception cref="FormatException">Thrown when an unknown encoding
		/// (other than Q-Encoding or Base64) is encountered.</exception>
		/// <returns>A concatenation of all enconded-words in the passed
		/// string</returns>
		public static string DecodeWords(string words) {
			MatchCollection matches = Regex.Matches(words,
				@"(=\?[A-Za-z0-9\-]+\?[BbQq]\?[^\?]+\?=)");
			string decoded = String.Empty;
			foreach (Match m in matches)
				decoded = decoded + DecodeWord(m.ToString());
			return decoded;
		}

		/// <summary>
		/// Decodes a MIME 'encoded-word' string.
		/// </summary>
		/// <param name="word">The encoded word to decode</param>
		/// <exception cref="FormatException">Thrown when an unknown encoding
		/// (other than Q-Encoding or Base64) is encountered.</exception>
		/// <returns>A decoded string</returns>
		/// <remarks>MIME encoded-word syntax is a way to encode strings that
		/// contain non-ASCII data. Commonly used encodings for the encoded-word
		/// sytax are Q-Encoding and Base64. For an in-depth description, refer
		/// to RFC 2047</remarks>
		internal static string DecodeWord(string word) {
			Match m = Regex.Match(word,
					@"=\?([A-Za-z0-9\-]+)\?([BbQq])\?(.+)\?=");
			if (!m.Success)
				return word;
			Encoding encoding = null;
			try {
				encoding = Encoding.GetEncoding(
							m.Groups[1].Value);
			} catch (ArgumentException) {
				encoding = Encoding.ASCII;
			}
			string type = m.Groups[2].Value.ToUpper();
			string text = m.Groups[3].Value;
			switch (type) {
				case "Q":
					return Util.QDecode(text, encoding);
				case "B":
					return encoding.GetString(Util.Base64Decode(text));
				default:
					throw new FormatException("Encoding not recognized " +
						"in encoded word: " + word);
			}
		}

		/// <summary>
		/// Takes a Q-encoded string and decodes it using the specified
		/// encoding.
		/// </summary>
		/// <param name="value">The Q-encoded string to decode</param>
		/// <param name="encoding">The encoding to use for encoding the
		/// returned string</param>
		/// <returns>A Q-decoded string</returns>
		internal static string QDecode(string value, Encoding encoding) {
			MatchCollection matches = Regex.Matches(value, @"=[0-9A-Z]{2}",
				RegexOptions.Multiline);
			foreach (Match match in matches) {
				char hexChar = (char)Convert.ToInt32(
					match.Groups[0].Value.Substring(1), 16);
				value = value.Replace(match.Groups[0].Value, hexChar.ToString());
			}
			value = value.Replace("=\r\n", "").Replace("_", " ");
			return encoding.GetString(
				Encoding.Default.GetBytes(value));
		}

		/// <summary>
		/// Takes a quoted-printable-encoded string and decodes it using
		/// the specified encoding.
		/// </summary>
		/// <param name="value">The quoted-printable-encoded string to
		/// decode</param>
		/// <param name="encoding">The encoding to use for encoding the
		/// returned string</param>
		/// <returns>A quoted-printable-decoded string</returns>
		internal static string QPDecode(string value, Encoding encoding) {
			MatchCollection matches = Regex.Matches(value, @"=[0-9A-Z]{2}",
				RegexOptions.Multiline);
			foreach (Match match in matches) {
				char hexChar = (char)Convert.ToInt32(
					match.Groups[0].Value.Substring(1), 16);
				value = value.Replace(match.Groups[0].Value, hexChar.ToString());
			}
			value = value.Replace("=\r\n", "");
			return encoding.GetString(
				Encoding.Default.GetBytes(value));
		}

		/// <summary>
		/// Takes a Base64-encoded string and decodes it.
		/// </summary>
		/// <param name="value">The Base64-encoded string to decode</param>
		/// <returns>A byte array containing the Base64-decoded bytes
		/// of the input string.</returns>
		internal static byte[] Base64Decode(string value) {
			return Convert.FromBase64String(value);
		}
	}
}
