using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap {
	/// <summary>
	/// A helper class for parsing the BODYSTRUCTURE response of an
	/// IMAP FETCH command more conveniently.
	/// </summary>
	internal class Reader : StringReader {
		/// <summary>
		/// Initializes a new instance of the Reader class that reads from the
		/// specified string.
		/// </summary>
		/// <param name="s">The string to which the Reader instance should be
		/// initialized.</param>
		public Reader(string s)
			: base(s) {
		}

		/// <summary>
		/// Reads the next character from the input string and advances the
		/// character position by one character.
		/// </summary>
		/// <returns>The next character from the underlying string.</returns>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public override int Read() {
			int character = base.Read();
			if (character == -1)
				throw new EndOfStringException();
			return character;
		}

		/// <summary>
		/// Returns the next available character but does not consume it.
		/// </summary>
		/// <param name="skipSpaces">Set to true to skip any preceding
		/// whitespace characters.</param>
		/// <returns>An integer representing the next character to be read,
		/// or -1 if no more characters are available.</returns>
		public int Peek(bool skipSpaces) {
			try {
				if (skipSpaces)
					SkipSpaces();
			} catch (EndOfStringException) {
			}
			return base.Peek();
		}

		/// <summary>
		/// Advances the character position until the specified character
		/// is encountered.
		/// </summary>
		/// <param name="character">The character to skip to.</param>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public void SkipUntil(char character) {
			while (Read() != character) ;
		}

		/// <summary>
		/// Advances the character position over any whitespace characters
		/// and subsequently ensures the next read will not return a
		/// whitespace character.
		/// </summary>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public void SkipSpaces() {
			while (Peek() == ' ' || Peek() == '\t')
				Read();
		}

		/// <summary>
		/// Reads characters until the specified character is encountered.
		/// </summary>
		/// <param name="character">The character to read up to.</param>
		/// <returns>The read characters as a string value.</returns>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public string ReadUntil(char character) {
			StringBuilder builder = new StringBuilder();
			char c;
			while ((c = (char)Read()) != character)
				builder.Append(c);
			return builder.ToString();
		}

		/// <summary>
		/// Reads a word from the underlying string. A word in this context
		/// is a literal enclosed in double-quotes.
		/// </summary>
		/// <returns>The read word.</returns>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public string ReadWord() {
			char c;
			char[] last = new char[3];
			while ((c = (char)Read()) != '"') {
				last[0] = last[1];
				last[1] = last[2];
				last[2] = c;
				if ((new string(last)) == "NIL")
					return "";
			}
			StringBuilder builder = new StringBuilder();
			last[0] = ' ';
			while (true) {
				// Account for backslash-escaped double-quote characters.
				if ((c = (char)Read()) == '"' && last[0] != '\\')
					break;
				builder.Append(c);
				last[0] = c;
			}
			return builder.ToString();
		}

		/// <summary>
		/// Reads an integer from the underlying string.
		/// </summary>
		/// <returns>The read integer value.</returns>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public Int64 ReadInteger() {
			StringBuilder builder = new StringBuilder();
			SkipSpaces();
			do {
				char c = (char)Read();
				if (c >= '0' && c <= '9')
					builder.Append(c);
				else
					break;
			} while (Peek() >= '0' && Peek() <= '9');
			return Int64.Parse(builder.ToString());
		}

		/// <summary>
		/// Reads a list from the underlying string. A list in this context
		/// is a list of attribute/value literals (enclosed in double-quotes)
		/// enclosed in parenthesis.
		/// </summary>
		/// <returns>The read list as a dictionary with the attribute names
		/// as keys and attribute values as values.</returns>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public Dictionary<string, string> ReadList() {
			Dictionary<string, string> Dict =
				 new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			char c;
			char[] last = new char[3];
			while ((c = (char)Read()) != '(') {
				last[0] = last[1];
				last[1] = last[2];
				last[2] = c;
				if ((new string(last)) == "NIL")
					return Dict;
			}
			StringBuilder pairs = new StringBuilder();
			last[0] = ' ';
			// Attribute/Value literals my contain parentheses.
			bool inQuotes = false;
			while (Peek() > 0) {
				c = (char)Read();
				if (c == '"' && last[0] != '\\')
					inQuotes = !inQuotes;
				if (c == ')' && !inQuotes)
					break;
				last[0] = c;
				pairs.Append(c);
			}
			MatchCollection matches = Regex.Matches(pairs.ToString(), "\"([^\"]+)\"\\s+\"([^\"]+)\"");
			foreach (Match m in matches)
				Dict.Add(m.Groups[1].Value, m.Groups[2].Value);
			return Dict;
		}

		/// <summary>
		/// Reads a disposition from the underlying string. A disposition in
		/// this context is a list of attribute/value literals (enclosed in
		/// double-quotes) preceded by a word enclosed in parenthesis.
		/// </summary>
		/// <returns>An initialized ContentDisposition instance representing
		/// the parsed disposition.</returns>
		/// <exception cref="EndOfStringException">Thrown when reading is
		/// attempted past the end of the underlying string.</exception>
		public ContentDisposition ReadDisposition() {
			ContentDisposition Disp = new ContentDisposition();
			char c;
			char[] last = new char[3];
			while ((c = (char)Read()) != '(') {
				last[0] = last[1];
				last[1] = last[2];
				last[2] = c;
				if ((new string(last)) == "NIL")
					return Disp;
			}

			string type = ReadWord();
			Disp.Type = ContentDispositionTypeMap.fromString(type);
			Disp.Attributes = ReadList();
			ReadUntil(')');

			if (Disp.Attributes.ContainsKey("Filename"))
				Disp.Filename = Disp.Attributes["Filename"];
			return Disp;
		}
	}

	/// <summary>
	/// The exception that is thrown when reading is attempted past the end
	/// of a string.
	/// </summary>
	internal class EndOfStringException : Exception {
		/// <summary>
		/// Initializes a new instance of the EndOfStringException class
		/// </summary>
		public EndOfStringException() : base() { }
		/// <summary>
		/// Initializes a new instance of the EndOfStringException class with its message
		/// string set to <paramref name="message"/>.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		public EndOfStringException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the EndOfStringException class with its message
		/// string set to <paramref name="message"/> and a reference to the inner exception that
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">A description of the error. The content of message is intended
		/// to be understood by humans.</param>
		/// <param name="inner">The exception that is the cause of the current exception.</param>
		public EndOfStringException(string message, Exception inner) : base(message, inner) { }
		/// <summary>
		/// Initializes a new instance of the EndOfStringException class with the specified
		/// serialization and context information.
		/// </summary>
		/// <param name="info">An object that holds the serialized object data about the exception
		/// being thrown. </param>
		/// <param name="context">An object that contains contextual information about the source
		/// or destination. </param>
		protected EndOfStringException(SerializationInfo info, StreamingContext context) { }
	}
}
