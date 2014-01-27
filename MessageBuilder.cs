using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap {
	/// <summary>
	/// A helper class for reading mail message data and building a MailMessage instance out of it.
	/// </summary>
	internal static class MessageBuilder {
		/// <summary>
		/// Creates a new empty instance of the MailMessage class from a string containing a raw mail
		/// message header.
		/// </summary>
		/// <param name="text">The mail header to create the MailMessage instance from.</param>
		/// <returns>A MailMessage instance with initialized Header fields but without any
		/// content.</returns>
		internal static MailMessage FromHeader(string text) {
			NameValueCollection header = ParseMailHeader(text);
			MailMessage m = new MailMessage();
			foreach (string key in header) {
				string value = header.GetValues(key)[0];
				try {
					m.Headers.Add(key, value);
				} catch {
					// HeaderCollection throws an exception if adding an empty string as value, which can
					// happen, if reading a mail message with an empty subject.
					// Also spammers often forge headers, so just fall through and ignore.
				}
			}
			Match ma = Regex.Match(header["Subject"] ?? "", @"=\?([A-Za-z0-9\-_]+)");
			if (ma.Success) {
				// encoded-word subject. A subject must not contain any encoded newline
				// characters, so if we find any, we strip them off.
				m.SubjectEncoding = Util.GetEncoding(ma.Groups[1].Value);
				try {
					m.Subject = Util.DecodeWords(header["Subject"]).
						Replace("\n", "").Replace("\r", "");
				} catch {
					// If, for any reason, decoding fails, set the subject to the
					// original, unaltered string.
					m.Subject = header["Subject"];
				}
			} else {
				m.SubjectEncoding = Encoding.ASCII;
				m.Subject = header["Subject"];
			}
			m.Priority = ParsePriority(header["Priority"]);
			SetAddressFields(m, header);
			return m;
		}

		/// <summary>
		/// Creates a new instance of the MailMessage class from a string containing raw RFC822/MIME
		/// mail message data.
		/// </summary>
		/// <param name="text">The mail message data to create the MailMessage instance from.</param>
		/// <returns>An initialized instance of the MailMessage class.</returns>
		/// <remarks>This is used when fetching entire messages instead of the partial-fetch mechanism
		/// because it saves redundant round-trips to the server.</remarks>
		internal static MailMessage FromMIME822(string text) {
			StringReader reader = new StringReader(text);
			StringBuilder header = new StringBuilder();
			string line;
			while (!String.IsNullOrEmpty(line = reader.ReadLine()))
				header.AppendLine(line);
			MailMessage m = FromHeader(header.ToString());
			MIMEPart[] parts = ParseMailBody(reader.ReadToEnd(), m.Headers);
			foreach (MIMEPart p in parts)
				m.AddBodypart(BodypartFromMIME(p), p.body);
			return m;
		}

		/// <summary>
		/// Parses the mail header of a mail message and returns it as a NameValueCollection.
		/// </summary>
		/// <param name="header">The mail header to parse.</param>
		/// <returns>A NameValueCollection containing the header fields as keys with their respective
		/// values as values.</returns>
		internal static NameValueCollection ParseMailHeader(string header) {
			StringReader reader = new StringReader(header);
			NameValueCollection coll = new NameValueCollection();
			string line, fieldname = null, fieldvalue = null;
			var exclude = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) {
				"Subject", "Comments", "Content-disposition", "User-Agent" };
			while ((line = reader.ReadLine()) != null) {
				if (line == String.Empty)
					continue;
				// Values may stretch over several lines.
				if (line[0] == ' ' || line[0] == '\t') {
					if (fieldname != null)
						coll[fieldname] = coll[fieldname] + line.TrimEnd();
					continue;
				}
				// The mail header consists of field:value pairs.
				int delimiter = line.IndexOf(':');
				if (delimiter < 0)
					continue;
				fieldname = line.Substring(0, delimiter).Trim();
				fieldvalue = line.Substring(delimiter + 1).Trim();
				// Strip comments from RFC822 and MIME fields unless they are unstructured fields.
				if (!exclude.Contains(fieldname))
					fieldvalue = StripComments(fieldvalue);
				coll.Add(fieldname, fieldvalue);
			}
			return coll;
		}

		/// <summary>
		/// Strips RFC822/MIME comments from the specified string.
		/// </summary>
		/// <param name="s">The string to strip comments from.</param>
		/// <returns>A new string stripped of any comments.</returns>
		internal static string StripComments(string s) {
			if (String.IsNullOrEmpty(s))
				return s;
			bool inQuotes = false, escape = false;
			char last = ' ';
			int depth = 0;
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];
				if (c == '\\' && !escape) {
					escape = true;
					continue;
				}
				if (c == '"' && !escape)
					inQuotes = !inQuotes;
				last = c;
				if (!inQuotes && !escape && c == '(')
					depth++;
				else if (!inQuotes && !escape && c == ')' && depth > 0)
					depth--;
				else if (depth <= 0)
					builder.Append(c);
				escape = false;
			}
			return builder.ToString().Trim();
		}

		/// <summary>
		/// Parses a MIME header field which can contain multiple 'parameter = value'
		/// pairs (such as Content-Type: text/html; charset=iso-8859-1).
		/// </summary>
		/// <param name="field">The header field to parse.</param>
		/// <returns>A NameValueCollection containing the parameter names as keys with the respective
		/// parameter values as values.</returns>
		/// <remarks>The value of the actual field disregarding the 'parameter = value' pairs is stored
		/// in the collection under the key "value" (in the above example of Content-Type, this would
		/// be "text/html").</remarks>
		static NameValueCollection ParseMIMEField(string field) {
			NameValueCollection coll = new NameValueCollection();
			var fixup = new HashSet<string>();
			try {
				// This accounts for MIME Parameter Value Extensions (RFC2231).
				MatchCollection matches = Regex.Matches(field,
					@"([\w\-]+)(?:\*\d{1,3})?(\*?)?\s*=\s*([^;]+)");
				foreach (Match m in matches) {
					string pname = m.Groups[1].Value.Trim(), pval = m.Groups[3].Value.Trim('"');
					coll[pname] = coll[pname] + pval;
					if (m.Groups[2].Value == "*")
						fixup.Add(pname);
				}
				foreach (var pname in fixup) {
					try {
						coll[pname] = Util.Rfc2231Decode(coll[pname]);
					} catch (FormatException) {
						// If decoding fails, we should at least return the un-altered value.
					}
				}
				Match mvalue = Regex.Match(field, @"^\s*([^;]+)");
				coll.Add("value", mvalue.Success ? mvalue.Groups[1].Value.Trim() : "");
			} catch {
				// We don't want this to blow up on the user with weird mails.
				coll.Add("value", String.Empty);
			}
			return coll;
		}

		/// <summary>
		/// Parses a mail header address-list field such as To, Cc and Bcc which can contain multiple
		/// email addresses.
		/// </summary>
		/// <param name="list">The address-list field to parse</param>
		/// <returns>An array of MailAddress objects representing the parsed mail addresses.</returns>
		internal static MailAddress[] ParseAddressList(string list) {
			List<MailAddress> mails = new List<MailAddress>();
			if (String.IsNullOrEmpty(list))
				return mails.ToArray();
			foreach (string part in SplitAddressList(list)) {
				MailAddressCollection mcol = new MailAddressCollection();
				try {
					// .NET won't accept address-lists ending with a ';' or a ',' character, see #68.
					mcol.Add(part.TrimEnd(';', ','));
					foreach (MailAddress m in mcol) {
						// We might still need to decode the display name if it is Q-encoded.
						string displayName = Util.DecodeWords(m.DisplayName);
						mails.Add(new MailAddress(m.Address, displayName));
					}
				} catch {
					// We don't want this to throw any exceptions even if the entry is malformed.
				}
			}
			return mails.ToArray();
		}

		/// <summary>
		/// Splits the specified address-list into individual parts consisting of a mail address and
		/// optionally a display-name.
		/// </summary>
		/// <param name="list">The address-list to split into parts.</param>
		/// <returns>An enumerable collection of parts.</returns>
		internal static IEnumerable<string> SplitAddressList(string list) {
			IList<string> parts = new List<string>();
			StringBuilder builder = new StringBuilder();
			bool inQuotes = false;
			char last = '.';
			for (int i = 0; i < list.Length; i++) {
				if (list[i] == '"' && last != '\\')
					inQuotes = !inQuotes;
				if (list[i] == ',' && !inQuotes) {
					parts.Add(builder.ToString().Trim());
					builder.Length = 0;
				} else {
					builder.Append(list[i]);
				}
				if (i == list.Length - 1)
					parts.Add(builder.ToString().Trim());
			}
			return parts;
		}

		/// <summary>
		/// Parses a mail message identifier from a string.
		/// </summary>
		/// <param name="field">The field to parse the message id from</param>
		/// <exception cref="ArgumentException">The field argument does not contain a valid message
		/// identifier.</exception>
		/// <returns>The parsed message id.</returns>
		/// <remarks>A message identifier (msg-id) is a globally unique identifier for a
		/// message.</remarks>
		static string ParseMessageId(string field) {
			// A msg-id is enclosed in < > brackets.
			Match m = Regex.Match(field, @"<(.+)>");
			if (m.Success)
				return m.Groups[1].Value;
			throw new ArgumentException("The field does not contain a valid message " +
				"identifier: " + field);
		}

		/// <summary>
		/// Parses the priority of a mail message which can be specified as part of the header
		/// information.
		/// </summary>
		/// <param name="priority">The mail header priority value. The value can be null in which case
		/// a "normal priority" is returned.</param>
		/// <returns>A value from the MailPriority enumeration corresponding to the specified mail
		/// priority. If the passed priority value is null or invalid, a normal priority is assumed and
		/// MailPriority.Normal is returned.</returns>
		static MailPriority ParsePriority(string priority) {
			Dictionary<string, MailPriority> Map =
				new Dictionary<string, MailPriority>(StringComparer.OrdinalIgnoreCase) {
						{ "non-urgent", MailPriority.Low },
						{ "normal",	MailPriority.Normal },
						{ "urgent",	MailPriority.High }
				};
			try {
				return Map[priority];
			} catch {
				return MailPriority.Normal;
			}
		}

		/// <summary>
		/// Sets the address fields (From, To, CC, etc.) of a MailMessage object using the specified
		/// mail message header information.
		/// </summary>
		/// <param name="m">The MailMessage instance to operate on.</param>
		/// <param name="header">A collection of mail and MIME headers.</param>
		static void SetAddressFields(MailMessage m, NameValueCollection header) {
			MailAddress[] addr;
			if (header["To"] != null) {
				addr = ParseAddressList(header["To"]);
				foreach (MailAddress a in addr)
					m.To.Add(a);
			}
			if (header["Cc"] != null) {
				addr = ParseAddressList(header["Cc"]);
				foreach (MailAddress a in addr)
					m.CC.Add(a);
			}
			if (header["Bcc"] != null) {
				addr = ParseAddressList(header["Bcc"]);
				foreach (MailAddress a in addr)
					m.Bcc.Add(a);
			}
			if (header["From"] != null) {
				addr = ParseAddressList(header["From"]);
				if (addr.Length > 0)
					m.From = addr[0];
			}
			if (header["Sender"] != null) {
				addr = ParseAddressList(header["Sender"]);
				if (addr.Length > 0)
					m.Sender = addr[0];
			}
			if (header["Reply-to"] != null) {
				addr = ParseAddressList(header["Reply-to"]);
				// The ReplayToList property has only been part of the MailMessage class since .NET 4.0.
#if !NET35
				foreach (MailAddress a in addr)
					m.ReplyToList.Add(a);
#else
				if (addr.Length > 0)
					m.ReplyTo = addr[0];
#endif
			}
		}

		/// <summary>
		/// Adds a body part to an existing MailMessage instance.
		/// </summary>
		/// <param name="message">Extension method for the MailMessage class.</param>
		/// <param name="part">The body part to add to the MailMessage instance.</param>
		/// <param name="content">The content of the body part.</param>
		internal static void AddBodypart(this MailMessage message, Bodypart part, string content) {
			Encoding encoding = part.Parameters.ContainsKey("Charset") ?
				Util.GetEncoding(part.Parameters["Charset"]) : Encoding.ASCII;
			// Decode the content if it is encoded.
			byte[] bytes;
			try {
				switch (part.Encoding) {
					case ContentTransferEncoding.QuotedPrintable:
						bytes = encoding.GetBytes(Util.QPDecode(content, encoding));
						break;
					case ContentTransferEncoding.Base64:
						bytes = Util.Base64Decode(content);
						break;
					default:
						bytes = Encoding.ASCII.GetBytes(content);
						break;
				}
			} catch {
				// If it's not a valid Base64 or quoted-printable encoded string just leave the data as is.
				bytes = Encoding.ASCII.GetBytes(content);
			}

			// If the part has a name it most likely is an attachment and it should go into the
			// Attachments collection.
			bool hasName = part.Parameters.ContainsKey("name");

			// If the MailMessage's Body fields haven't been initialized yet, put it there. Some weird
			// (i.e. spam) mails like to omit content-types so we don't check for that here and just
			// assume it's text.
			if (String.IsNullOrEmpty(message.Body) &&
				part.Disposition.Type != ContentDispositionType.Attachment) {
				message.Body = encoding.GetString(bytes);
				message.BodyEncoding = encoding;
				message.IsBodyHtml = part.Subtype.ToLower() == "html";
				return;
			}

			// Check for alternative view.
			string ContentType = ParseMIMEField(message.Headers["Content-Type"])["value"];
			bool preferAlternative = string.Compare(ContentType, "multipart/alternative", true) == 0;

			// Many attachments are missing the disposition-type. If it's not defined as alternative
			// and it has a name attribute, assume it is Attachment rather than an AlternateView.
			if (part.Disposition.Type == ContentDispositionType.Attachment ||
				(part.Disposition.Type == ContentDispositionType.Unknown &&
				preferAlternative == false && hasName))
				message.Attachments.Add(CreateAttachment(part, bytes));
			else
				message.AlternateViews.Add(CreateAlternateView(part, bytes));
		}

		/// <summary>
		/// Creates an instance of the Attachment class used by the MailMessage class to store mail
		/// message attachments.
		/// </summary>
		/// <param name="part">The MIME body part to create the attachment from.</param>
		/// <param name="bytes">An array of bytes composing the content of the attachment.</param>
		/// <returns>An initialized instance of the Attachment class.</returns>
		static Attachment CreateAttachment(Bodypart part, byte[] bytes) {
			MemoryStream stream = new MemoryStream(bytes);
			string name = part.Disposition.Filename;
			// Many MUAs put the file name in the name parameter of the content-type header instead of
			// the filename parameter of the content-disposition header.
			if (String.IsNullOrEmpty(name) && part.Parameters.ContainsKey("name"))
				name = part.Parameters["name"];
			if (String.IsNullOrEmpty(name))
				name = Path.GetRandomFileName();
			Attachment attachment = new Attachment(stream, name);
			try {
				attachment.ContentId = ParseMessageId(part.Id);
			} catch { }
			try {
				attachment.ContentType = new System.Net.Mime.ContentType(
					part.Type.ToString().ToLower() + "/" + part.Subtype.ToLower());
			} catch {
				attachment.ContentType = new System.Net.Mime.ContentType();
			}
			// Workaround: filename from Attachment constructor is ignored with Mono.
			attachment.Name = name;
			attachment.ContentDisposition.FileName = name;
			return attachment;
		}

		/// <summary>
		/// Creates an instance of the AlternateView class used by the MailMessage class to store
		/// alternate views of the mail message's content.
		/// </summary>
		/// <param name="part">The MIME body part to create the alternate view from.</param>
		/// <param name="bytes">An array of bytes composing the content of the alternate view.</param>
		/// <returns>An initialized instance of the AlternateView class.</returns>
		static AlternateView CreateAlternateView(Bodypart part, byte[] bytes) {
			MemoryStream stream = new MemoryStream(bytes);
			System.Net.Mime.ContentType contentType;
			try {
				contentType = new System.Net.Mime.ContentType(
					part.Type.ToString().ToLower() + "/" + part.Subtype.ToLower());
			} catch {
				contentType = new System.Net.Mime.ContentType();
			}
			AlternateView view = new AlternateView(stream, contentType);
			try {
				view.ContentId = ParseMessageId(part.Id);
			} catch { }
			return view;
		}

		/// <summary>
		/// Parses the body part of a MIME/RFC822 mail message.
		/// </summary>
		/// <param name="body">The body of the mail message.</param>
		/// <param name="header">The header of the mail message whose body will be parsed.</param>
		/// <returns>An array of initialized MIMEPart instances representing the body parts of the mail
		/// message.</returns>
		static MIMEPart[] ParseMailBody(string body,
			NameValueCollection header) {
			NameValueCollection contentType = ParseMIMEField(header["Content-Type"]);
			if (contentType["Boundary"] != null) {
				return ParseMIMEParts(new StringReader(body), contentType["Boundary"]);
			} else {
				return new MIMEPart[] {
					new MIMEPart() { body = body,
						header = new NameValueCollection() {
							{ "Content-Type", header["Content-Type"] },
							{ "Content-Id", header["Content-Id"] },
							{ "Content-Transfer-Encoding", header["Content-Transfer-Encoding"] },
							{ "Content-Disposition", header["Content-Disposition"] }
						}
					}
				};
			}
		}

		/// <summary>
		/// Parses the body of a multipart MIME mail message.
		/// </summary>
		/// <param name="reader">An instance of the StringReader class initialized with a string
		/// containing the body of the mail message.</param>
		/// <param name="boundary">The boundary value as is present as part of the Content-Type header
		/// field in multipart mail messages.</param>
		/// <returns>An array of initialized MIMEPart instances representing the various parts of the
		/// MIME mail message.</returns>
		static MIMEPart[] ParseMIMEParts(StringReader reader, string boundary) {
			List<MIMEPart> list = new List<MIMEPart>();
			string start = "--" + boundary, end = "--" + boundary + "--", line;
			// Skip everything up to the first boundary.
			while ((line = reader.ReadLine()) != null) {
				if (line.StartsWith(start))
					break;
			}
			// Read the MIME parts which are delimited by boundary strings.
			while (line != null && line.StartsWith(start)) {
				MIMEPart p = new MIMEPart();
				// Read the part header.
				StringBuilder header = new StringBuilder();
				while (!String.IsNullOrEmpty(line = reader.ReadLine()))
					header.AppendLine(line);
				p.header = ParseMailHeader(header.ToString());
				// Account for nested multipart content.
				NameValueCollection contentType = ParseMIMEField(p.header["Content-Type"]);
				if (contentType["Boundary"] != null)
					list.AddRange(ParseMIMEParts(reader, contentType["boundary"]));
				// Read the part body.
				StringBuilder body = new StringBuilder();
				while ((line = reader.ReadLine()) != null) {
					if (line.StartsWith(start))
						break;
					body.AppendLine(line);
				}
				p.body = body.ToString();
				// Add the MIME part to the list unless body is null or empty which means the body
				// contained nested multipart content.
				if(p.body != null && p.body.Trim() != String.Empty)
					list.Add(p);
				// If this boundary is the end boundary, we're done.
				if (line == null || line.StartsWith(end))
					break;
			}
			return list.ToArray();
		}

		/// <summary>
		/// Glue method to create a bodypart from a MIMEPart instance.
		/// </summary>
		/// <param name="mimePart">The MIMEPart instance to create the bodypart instance from.</param>
		/// <returns>An initialized instance of the Bodypart class.</returns>
		static Bodypart BodypartFromMIME(MIMEPart mimePart) {
			NameValueCollection contentType = ParseMIMEField(
				mimePart.header["Content-Type"]);
			Bodypart p = new Bodypart(null);
			Match m = Regex.Match(contentType["value"], "(.+)/(.+)");
			if (m.Success) {
				p.Type = ContentTypeMap.fromString(m.Groups[1].Value);
				p.Subtype = m.Groups[2].Value;
			}
			p.Encoding = ContentTransferEncodingMap.fromString(
				mimePart.header["Content-Transfer-Encoding"]);
			p.Id = mimePart.header["Content-Id"];
			foreach (string k in contentType.AllKeys)
				p.Parameters.Add(k, contentType[k]);
			p.Size = mimePart.body.Length;
			if (mimePart.header["Content-Disposition"] != null) {
				NameValueCollection disposition = ParseMIMEField(
					mimePart.header["Content-Disposition"]);
				p.Disposition.Type = ContentDispositionTypeMap.fromString(
					disposition["value"]);
				p.Disposition.Filename = disposition["Filename"];
				foreach (string k in disposition.AllKeys)
					p.Disposition.Attributes.Add(k, disposition[k]);
			}
			return p;
		}
	}
}