using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap {
	/// <summary>
	/// A static utility class containing methods for decoding encoded non-ASCII data as well as
	/// extension methods for some classes of the base class library.
	/// </summary>
	internal static class Util {
		/// <summary>
		/// Returns a copy of the string enclosed in double-quotes and with escaped CRLF, back-slash
		/// and double-quote characters (as is expected by some commands of the IMAP protocol).
		/// </summary>
		/// <param name="value">Extends the System.String class</param>
		/// <returns>A copy of the string enclosed in double-quotes and properly escaped as is required
		/// by the IMAP protocol.</returns>
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
		/// <returns>true if the string contains only ASCII characters; Otherwise false.</returns>
		internal static bool IsASCII(this string s) {
			return s.All(c => c < 127);
		}

		/// <summary>
		/// Splits a string into chunks of the specified number of characters.
		/// </summary>
		/// <param name="str">Extension method for the String class.</param>
		/// <param name="characters">The length of a chunk, measured in characters.</param>
		/// <returns>An array of string chunks.</returns>
		[Obsolete]
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
		/// Determines whether the specified string occurs within this string.
		/// </summary>
		/// <param name="str">Extension method for the String class.</param>
		/// <param name="value">The string to seek.</param>
		/// <param name="comparer">One of the enumeration values that specifies the rules for the
		/// search.</param>
		/// <returns>true if the value parameter occurs within this string, or if value is the empty
		/// string (""); otherwise, false.</returns>
		/// <exception cref="ArgumentNullException">The value parameter is null.</exception>
		internal static bool Contains(this string str, string value, StringComparison comparer) {
			return str.IndexOf(value, comparer) >= 0;
		}

		/// <summary>
		/// Raises the event. Ensures the event is only raised, if it is not null.
		/// </summary>
		/// <typeparam name="T">Extends System.EventHandler class.</typeparam>
		/// <param name="event">Extends System.EventHandler class.</param>
		/// <param name="sender">The sender of the event.</param>
		/// <param name="args">The event arguments associated with this event.</param>
		internal static void Raise<T>(this EventHandler<T> @event, object sender, T args)
			where T : EventArgs {
			EventHandler<T> handler = @event;
			if (handler != null)
				handler(sender, args);
		}

		/// <summary>
		/// Throws an ArgumentNullException if the given data item is null.
		/// </summary>
		/// <param name="data">The item to check for nullity.</param>
		/// <param name="name">The name to use when throwing an exception, if necessary.</param>
		/// <remarks>Courtesy of Jon Skeet.</remarks>
		internal static void ThrowIfNull<T>(this T data, string name) where T : class {
			if (data == null)
				throw new ArgumentNullException(name);
		}

		/// <summary>
		/// Throws an ArgumentNullException if the given data item is null.
		/// </summary>
		/// <param name="data">The item to check for nullity.</param>
		/// <remarks>Courtesy of Jon Skeet.</remarks>
		internal static void ThrowIfNull<T>(this T data) where T : class {
			if (data == null)
				throw new ArgumentNullException();
		}

		/// <summary>
		/// Throws an ArgumentNullException if the given string is null or throws an ArgumentException
		/// if the given string is empty.
		/// </summary>
		/// <param name="s">The string to check for nullity and emptiness.</param>
		internal static void ThrowIfNullOrEmpty(this string s) {
			if (s == null)
				throw new ArgumentNullException();
			if (s == String.Empty)
				throw new ArgumentException();
		}

		/// <summary>
		/// Throws an ArgumentNullException if the given string is null or throws an ArgumentException
		/// if the given string is empty.
		/// </summary>
		/// <param name="s">The string to check for nullity and emptiness.</param>
		/// <param name="name">The name to use when throwing an exception, if necessary.</param>
		internal static void ThrowIfNullOrEmpty(this string s, string name) {
			if (s == null)
				throw new ArgumentNullException(name);
			if (s == String.Empty)
				throw new ArgumentException(name + " must not be empty.");
		}

		/// <summary>
		/// Reads an unsigned short value from the underlying stream, optionally using big-endian byte
		/// ordering.
		/// </summary>
		/// <param name="reader">Extension method for BinaryReader.</param>
		/// <param name="bigEndian">Set to true to interpret the short value as big endian value.</param>
		/// <returns>The 16-byte unsigned short value read from the underlying stream.</returns>
		internal static ushort ReadUInt16(this BinaryReader reader, bool bigEndian) {
			if (!bigEndian)
				return reader.ReadUInt16();
			int ret = 0;
			ret |= (reader.ReadByte() << 8);
			ret |= (reader.ReadByte() << 0);
			return (ushort) ret;
		}

		/// <summary>
		/// Decodes a string composed of one or several MIME 'encoded-words'.
		/// </summary>
		/// <param name="words">A string to composed of one or several MIME 'encoded-words'.</param>
		/// <exception cref="FormatException">An unknown encoding (other than Q-Encoding or Base64) is
		/// encountered.</exception>
		/// <returns>A concatenation of all enconded-words in the passed string</returns>
		public static string DecodeWords(string words) {
			if (String.IsNullOrEmpty(words))
				return String.Empty;
			MatchCollection matches = rxDecodeWord.Matches(words);
			if (matches.Count == 0)
				return words;
			// http://tools.ietf.org/html/rfc2047#page-10:
			// When displaying a particular header field that contains multiple 'encoded-word's, any
			// 'linear-white-space' that separates a pair of adjacent 'encoded-word's is ignored. (This is
			// to allow the use of multiple 'encoded-word's to represent long strings of unencoded text,
			// without having to separate 'encoded-word's where spaces occur in the unencoded text.)
			// line-white-space ref: http://tools.ietf.org/html/rfc2616#page-16
			StringBuilder decoded = new StringBuilder();
			// Keep track of and use separation data between 'encoded-word's.
			int LastKnownMatchPos = 0;
			foreach (Match m in matches) {
				if (m.Index > LastKnownMatchPos)
					HandleFillData(decoded, words.Substring(LastKnownMatchPos, m.Index -
						LastKnownMatchPos));
				decoded.Append(DecodeWord(m.Groups[0].Value));
				LastKnownMatchPos = m.Index + m.Length;
			}
			HandleFillData(decoded, words.Substring(LastKnownMatchPos));
			return decoded.ToString();
		}

		/// <summary>
		/// Internal function reuse to add separation between multiple 'encoded-word's correctly.
		/// </summary>
		static void HandleFillData(StringBuilder decoded, string data) {
			if (String.IsNullOrEmpty(data))
				return;
			// Cr or Lf is never in the result.
			string FillData = data.Replace("\r", "").Replace("\n", "");
			// Any 'linear-white-space' that separates a pair of adjacent 'encoded-word's is ignored.
			if (FillData.Trim().Length != 0)
				decoded.Append(FillData);
			}

		/// <summary>
		/// The regular expression for matching encoded-words. This also accounts for the extension for
		/// including language information (as defined in RFC2231).
		/// </summary>
		static readonly Regex rxDecodeWord =
			new Regex(@"=\?([A-Za-z0-9\-_]+)(?:\*[^\?]+)?\?([BbQq])\?([^\?]+)\?=", RegexOptions.Compiled);

		/// <summary>
		/// Decodes a MIME 'encoded-word' string.
		/// </summary>
		/// <param name="word">The encoded word to decode</param>
		/// <exception cref="FormatException">An unknown encoding (other than Q-Encoding or Base64) is
		/// encountered.</exception>
		/// <returns>A decoded string</returns>
		/// <remarks>MIME encoded-word syntax is a way to encode strings that contain non-ASCII data.
		/// Commonly used encodings for the encoded-word sytax are Q-Encoding and Base64. For an
		/// in-depth description, refer to RFC 2047.</remarks>
		internal static string DecodeWord(string word) {
			if (String.IsNullOrEmpty(word))
				return String.Empty;
			Match m = rxDecodeWord.Match(word);
			if (!m.Success)
				return word;
			Encoding encoding = Util.GetEncoding(m.Groups[1].Value);
			string type = m.Groups[2].Value.ToUpper();
			string text = m.Groups[3].Value;
			switch (type) {
				case "Q":
					return Util.QDecode(text, encoding);
				case "B":
					return encoding.GetString(Util.Base64Decode(text));
				default:
					throw new FormatException("Encoding not recognized in encoded word: " + word);
			}
		}

		/// <summary>
		/// Takes a Q-encoded string and decodes it using the specified encoding.
		/// </summary>
		/// <param name="value">The Q-encoded string to decode.</param>
		/// <param name="encoding">The encoding to use for encoding the returned string.</param>
		/// <exception cref="FormatException">The string is not a valid Q-encoded string.</exception>
		/// <returns>The Q-decoded string.</returns>
		internal static string QDecode(string value, Encoding encoding) {
			try {
				using (MemoryStream m = new MemoryStream()) {
					for (int i = 0; i < value.Length; i++) {
						if (value[i] == '=') {
							string hex = value.Substring(i + 1, 2);
							m.WriteByte(Convert.ToByte(hex, 16));
							i = i + 2;
						} else if (value[i] == '_') {
							m.WriteByte(Convert.ToByte(' '));
						} else {
							m.WriteByte(Convert.ToByte(value[i]));
						}
					}
					return encoding.GetString(m.ToArray());
				}
			} catch {
				throw new FormatException("value is not a valid Q-encoded string.");
			}
		}

		/// <summary>
		/// Takes a quoted-printable encoded string and decodes it using the specified encoding.
		/// </summary>
		/// <param name="value">The quoted-printable-encoded string to decode.</param>
		/// <param name="encoding">The encoding to use for encoding the returned string.</param>
		/// <exception cref="FormatException">The string is not a valid quoted-printable encoded
		/// string.</exception>
		/// <returns>A quoted-printable decoded string.</returns>
		internal static string QPDecode(string value, Encoding encoding) {
			try {
				using (MemoryStream m = new MemoryStream()) {
					for (int i = 0; i < value.Length; i++) {
						if (value[i] == '=') {
							string hex = value.Substring(i + 1, 2);
							// Deal with soft line breaks.
							if(hex != "\r\n")
								m.WriteByte(Convert.ToByte(hex, 16));
							i = i + 2;
						} else {
							m.WriteByte(Convert.ToByte(value[i]));
						}
					}
					return encoding.GetString(m.ToArray());
				}
			} catch {
				throw new FormatException("The value is not a valid quoted-printable encoded string.");
			}
		}

		/// <summary>
		/// Decodes the specified MIME parameter value.
		/// </summary>
		/// <param name="value">The MIME parameter value to decode.</param>
		/// <returns>The decoded MIME parameter value.</returns>
		/// <exception cref="FormatException">The specified value is not a valid Rfc2231-encoded
		/// string.</exception>
		/// <remarks>Refer to RFC2231 for the details of the encoding mechanism.</remarks>
		internal static string Rfc2231Decode(string value) {
			if (String.IsNullOrEmpty(value))
				return String.Empty;
			Match m = Regex.Match(value, @"^([\w\-]+)(?:\'[\w\-]*\')?(.*)");
			if (!m.Success)
				return value;
			string charset = m.Groups[1].Value, text = m.Groups[2].Value;
			try {
				Encoding enc = Encoding.GetEncoding(charset);
				using (MemoryStream ms = new MemoryStream()) {
					for (int i = 0; i < text.Length; i++) {
						if (text[i] == '%') {
							string hex = text.Substring(i + 1, 2);
							ms.WriteByte(Convert.ToByte(hex, 16));
							i = i + 2;
						} else
							ms.WriteByte(Convert.ToByte(text[i]));
					}
					return enc.GetString(ms.ToArray());
				}
			} catch {
				throw new FormatException("The value is not a valid RFC2231-encoded string.");
			}
		}

		/// <summary>
		/// Takes a Base64-encoded string and decodes it.
		/// </summary>
		/// <param name="value">The Base64-encoded string to decode.</param>
		/// <returns>A byte array containing the Base64-decoded bytes of the input string.</returns>
		/// <exception cref="System.ArgumentNullException">The value parameter is null.</exception>
		/// <exception cref="System.FormatException">The length of value, ignoring white-space
		/// characters, is not zero or a multiple of 4, or the format of value is invalid, or the value
		/// contains a non-base-64 character, more than two padding characters, or a non-white
		/// space-character among the padding characters.</exception>
		internal static byte[] Base64Decode(string value) {
			return Convert.FromBase64String(value);
		}

		/// <summary>
		/// Takes a UTF-16 encoded string and encodes it as modified UTF-7.
		/// </summary>
		/// <param name="s">The string to encode.</param>
		/// <returns>A UTF-7 encoded string</returns>
		/// <remarks>IMAP uses a modified version of UTF-7 for encoding international mailbox names. For
		/// details, refer to RFC 3501 section 5.1.3 (Mailbox International Naming Convention).</remarks>
		internal static string UTF7Encode(string s) {
			StringReader reader = new StringReader(s);
			StringBuilder builder = new StringBuilder();
			while (reader.Peek() != -1) {
				char c = (char)reader.Read();
				int codepoint = Convert.ToInt32(c);
				// It's a printable ASCII character.
				if (codepoint > 0x1F && codepoint < 0x80) {
					builder.Append(c == '&' ? "&-" : c.ToString());
				} else {
					// The character sequence needs to be encoded.
					StringBuilder sequence = new StringBuilder(c.ToString());
					while (reader.Peek() != -1) {
						codepoint = Convert.ToInt32((char)reader.Peek());
						if (codepoint > 0x1F && codepoint < 0x80)
							break;
						sequence.Append((char)reader.Read());
					}
					byte[] buffer = Encoding.BigEndianUnicode.GetBytes(
						sequence.ToString());
					string encoded = Convert.ToBase64String(buffer).Replace('/', ',').
						TrimEnd('=');
					builder.Append("&" + encoded + "-");
				}
			}
			return builder.ToString();
		}

		/// <summary>
		/// Takes a modified UTF-7 encoded string and decodes it.
		/// </summary>
		/// <param name="s">The UTF-7 encoded string to decode.</param>
		/// <returns>A UTF-16 encoded "standard" C# string</returns>
		/// <exception cref="FormatException">The input string is not a properly UTF-7 encoded
		/// string.</exception>
		/// <remarks>IMAP uses a modified version of UTF-7 for encoding international mailbox names. For
		/// details, refer to RFC 3501 section 5.1.3 (Mailbox International Naming Convention).</remarks>
		internal static string UTF7Decode(string s) {
			StringReader reader = new StringReader(s);
			StringBuilder builder = new StringBuilder();
			while (reader.Peek() != -1) {
				char c = (char)reader.Read();
				if (c == '&' && reader.Peek() != '-') {
					// The character sequence needs to be decoded.
					StringBuilder sequence = new StringBuilder();
					while (reader.Peek() != -1) {
						if ((c = (char)reader.Read()) == '-')
							break;
						sequence.Append(c);
					}
					string encoded = sequence.ToString().Replace(',', '/');
					int pad = encoded.Length % 4;
					if (pad > 0)
						encoded = encoded.PadRight(encoded.Length + (4 - pad), '=');
					try {
						byte[] buffer = Convert.FromBase64String(encoded);
						builder.Append(Encoding.BigEndianUnicode.GetString(buffer));
					} catch (Exception e) {
						throw new FormatException(
							"The input string is not in the correct Format.", e);
					}
				} else {
					if (c == '&' && reader.Peek() == '-')
						reader.Read();
					builder.Append(c);
				}
			}
			return builder.ToString();
		}

		/// <summary>
		/// This just wraps Encoding.GetEncoding in a try-catch block to ensure it never fails. If the
		/// encoding can not be determined ASCII is returned as a default.
		/// </summary>
		/// <param name="name">The code page name of the preferred encoding. Any value returned by
		/// System.Text.Encoding.WebName is a valid input.</param>
		/// <returns>The System.Text.Encoding associated with the specified code page or Encoding.ASCII
		/// if the specified code page could not be resolved.</returns>
		internal static Encoding GetEncoding(string name) {
			Encoding encoding;
			try {
				encoding = Encoding.GetEncoding(name);
			} catch {
				encoding = Encoding.ASCII;
			}
			return encoding;
		}

		/// <summary>
		/// Converts the specified enumerable collection of UIDs into an IMAP sequence-set.
		/// </summary>
		/// <param name="uids">An enumerable collection of UIDs.</param>
		/// <returns>An IMAP sequence-set suitable for use with IMAP commands.</returns>
		/// <exception cref="ArgumentNullException">The uids parameter is null.</exception>
		/// <exception cref="ArgumentException">The specified collection of UIDs is empty.</exception>
		/// <remarks> Refer to RFC3501, 9.Formal Syntax "sequence-set" for a description of IMAP
		/// sequence-sets.</remarks>
		internal static string BuildSequenceSet(IEnumerable<uint> uids) {
			uids.ThrowIfNull("uids");
			if (uids.Count() == 0)
				throw new ArgumentException("The specified collection is empty.");
			List<uint> list = uids.ToList<uint>();
			if (list.Count == 1)
				return list[0].ToString();
			list.Sort();
			int count = uids.Count();
			IList<uint> fromNums = new List<uint>(), toNums = new List<uint>();
			for (int i = 0; i < count - 1; i++) {
				if (i == 0)
					fromNums.Add(list[i]);
				if (list[i + 1] > list[i] + 1) {
					toNums.Add(list[i]);
					fromNums.Add(list[i + 1]);
				}
			}
			toNums.Add(list[count - 1]);
			var ranges = Enumerable.Range(0, toNums.Count).Select(
				i => fromNums[i] + (toNums[i] == fromNums[i] ? "" : ":" + toNums[i])).ToArray();
			return string.Join(",", ranges);
		}
	}
}
