using System;
using System.Collections.Generic;

namespace S22.Imap {
	/// <summary>
	/// Provides a means for parsing the textual description of the body structure of a mail
	/// message as is returned by an IMAP server for a "FETCH BODYSTRUCTURE" command.
	/// </summary>
	/// <remarks>
	/// They couldn't have made the BODYSTRUCTURE any more complicated and unnecessarily
	/// hard to parse. I wonder what they were thinking when they came up with this.
	/// </remarks>
	internal class Bodystructure {
		/// <summary>
		/// Parses the body structure of a mail message as is returned by the IMAP server
		/// in response to a FETCH BODYSTRUCTURE command.
		/// </summary>
		/// <param name="text">The body structure server response</param>
		/// <returns>An array of initialized Bodypart objects representing the body
		/// structure of the mail message</returns>
		/// <exception cref="FormatException">Thrown if the passed string does not
		/// contain a valid body structure and parsing failed.</exception>
		public static Bodypart[] Parse(string text) {
			Bodystructure s = new Bodystructure(text);
			List<Bodypart> list = new List<Bodypart>();
			try {
				char c = (char)s.reader.Peek();
				if (c == '(')
					list.AddRange(s.ParseList());
				else
					list.Add(s.ParseBodypart("1", false));
			} catch (Exception e) {
				throw new FormatException(text, e);
			}
			return list.ToArray();
		}

		/// <summary>
		/// A Reader object initialized with the string containing the bodystructure
		/// response.
		/// </summary>
		private Reader reader;

		/// <summary>
		/// Initializes a new instance of the Bodystructure class.
		/// </summary>
		/// <param name="text"></param>
		private Bodystructure(string text) {
			reader = new Reader(text);
		}

		/// <summary>
		/// Parses a bodypart entry from the body structure and advances the
		/// read pointer.
		/// </summary>
		/// <param name="partNumber">The designated part specifier by which the body
		/// part is refered to by the server.</param>
		/// <param name="parenthesis">Set to true if the bodypart is enclosed
		/// in parenthesis.</param>
		/// <returns></returns>
		private Bodypart ParseBodypart(string partNumber, bool parenthesis = true) {
			Bodypart part = new Bodypart(partNumber);
			// Mandatory fields:
			//  "Type"² "Subtype"² ("Attribute" "Value")² "Id"² "Description"² "Encoding"² Size³
			//  ² String value, but can be NIL, ³ Integer value
			part.Type = ContentTypeMap.fromString(reader.ReadWord());
			part.Subtype = reader.ReadWord();
			part.Parameters = reader.ReadList();
			part.Id = reader.ReadWord();
			part.Description = reader.ReadWord();
			part.Encoding = ContentTransferEncodingMap.fromString(reader.ReadWord());
			part.Size = reader.ReadInteger();
			if (part.Type == ContentType.Text)
				part.Lines = reader.ReadInteger();
			if (part.Type == ContentType.Message && part.Subtype.ToUpper() == "RFC822")
				ParseMessage822Fields(part);
			try {
				ParseOptionalFields(part, parenthesis);
			} catch (EndOfStringException) {}
			return part;
		}

		/// <summary>
		/// Parses the mandatory extra fields that are present if the bodypart is
		/// of type message/rfc822 (see RFC 3501, p. 75).
		/// </summary>
		/// <param name="part">The bodypart instance the parsed fields will be
		/// added to.</param>
		private void ParseMessage822Fields(Bodypart part) {
			// We just skip over most of this extra information as it is useless
			// to us.
			// Mandatory fields:
			//	"Envelope" "Bodystructure" "Lines"
			SkipParenthesizedExpression();
			SkipParenthesizedExpression();
			part.Lines = reader.ReadInteger();
		}

		/// <summary>
		/// Parses the optional fields of a bodypart entry from the body structure
		/// and advances the read pointer.
		/// </summary>
		/// <param name="part">The bodypart instance the parsed fields will be
		/// added to.</param>
		/// <param name="parenthesis">Set to true if the bodypart entry is enclosed
		/// in parenthesis.</param>
		private void ParseOptionalFields(Bodypart part, bool parenthesis = true) {
			// Optional fields:
			//  "Md5"² ("Disposition" ("Attribute" "Value"))² "Language"² "Location"²
			if (parenthesis && reader.Peek(true) == ')') {
				reader.Read();
				return;
			}
			part.Md5 = reader.ReadWord();
			if (parenthesis && reader.Peek(true) == ')') {
				reader.Read();
				return;
			}
			part.Disposition = reader.ReadDisposition();
			if (parenthesis && reader.Peek(true) == ')') {
				reader.Read();
				return;
			}
			part.Language = reader.ReadWord();
			if (parenthesis && reader.Peek(true) == ')') {
				reader.Read();
				return;
			}
			part.Location = reader.ReadWord();
			if (parenthesis)
				reader.SkipUntil(')');
		}

		/// <summary>
		/// Parses a list of bodypart entries as is outlined in the description of the
		/// BODYPART response in RFC 3501.
		/// </summary>
		/// <param name="nestPrefix">The nesting prefix that will be prefixed to the
		/// bodyparts partNumber.</param>
		/// <returns>An array of initialized Bodypart objects parsed from the
		/// list.</returns>
		private Bodypart[] ParseList(string nestPrefix = "") {
			List<Bodypart> list = new List<Bodypart>();
			int count = 1;

			// Consume opening bracket; If the next character is another opening
			// bracket, it's a nested list.
			while (reader.Peek(true) == '(') {
				reader.Read();
				char c = (char)reader.Peek(true);
				if (c == '(')
					list.AddRange(ParseList(nestPrefix + count + "."));
				else {
					string partNumber = nestPrefix + count;
					Bodypart part = ParseBodypart(partNumber);
					list.Add(part);
				}
				count = count + 1;
			}
			// Skip over multipart information.
			SkipMultipart();
			return list.ToArray();
		}

		/// <summary>
		/// Advances the read pointer to skip over a multipart entry.
		/// </summary>
		private void SkipMultipart() {
			int openingBrackets = 0;
			while (reader.Peek() > 0) {
				char c = (char)reader.Read();
				if (c == '(')
					openingBrackets++;
				if (c == ')') {
					if (openingBrackets == 0)
						break;
					openingBrackets--;
				}
			}
		}

		/// <summary>
		/// Advances the read pointer to skip over an arbitrary
		/// expression enclosed in parentheses.
		/// </summary>
		private void SkipParenthesizedExpression() {
			int openingBrackets = 1;
			bool inQuotes = false;
			char[] last = new char[3];
			char c;
			while ((c = (char)reader.Read()) != '(') {
				last[0] = last[1];
				last[1] = last[2];
				last[2] = c;
				if ((new string(last)) == "NIL")
					return;
			}
			while (reader.Peek() > 0) {
				c = (char)reader.Read();
				if (c == '"' && last[0] != '\\')
					inQuotes = !inQuotes;
				last[0] = c;
				if (c == '(' && !inQuotes)
					openingBrackets++;
				if (c == ')' && !inQuotes) {
					openingBrackets--;
					if (openingBrackets == 0)
						break;
				}
			}
		}
	}
}
