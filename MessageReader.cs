using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace S22.Imap {
	internal delegate string GetResponseDelegate();
	
	/// <summary>
	/// A helper class for reading a mail message and building a MailMessage
	/// instance out of it.
	/// </summary>
	/// <remarks>The MessageReader class has been deprecated as of version 2.0
	/// in favor of partial fetching of mail messages.</remarks>
	[Obsolete("Deprecated as of version 2.0 in favor of partial fetching")]
	internal class MessageReader {
		private GetResponseDelegate GetResponse;

		/// <summary>
		/// Initializes a new instance of the MessageReader class using the
		/// specified delegate.
		/// </summary>
		/// <param name="Delegate">A delegate to the GetResponse method which
		/// the MessageReader object invokes when it needs to read a line of
		/// data from the server.</param>
		public MessageReader(GetResponseDelegate Delegate) {
			GetResponse = Delegate;
		}

		/// <summary>
		/// Reads and processes the message data sent by the server and constructs
		/// a new MailMessage object from it.
		/// </summary>
		/// <param name="uid">The UID of the mail message whose data the server is about
		/// to send</param>
		/// <returns>An initialized instance of the MailMessage class representing the
		/// fetched mail message</returns>
		public MailMessage ReadMailMessage(uint uid) {
			NameValueCollection header = ReadMailHeader();
			NameValueCollection contentType = ParseMIMEField(
				header["Content-Type"]);
			string body = null;
			MIMEPart[] parts = null;
			if (contentType["boundary"] != null) {
				parts = ReadMultipartBody(contentType["boundary"]);
			} else {
				/* Content-Type does not contain a boundary, assume it's not
				 * a MIME multipart message then
				 */
				body = ReadMailBody();
			}
			return CreateMailmessage(header, body, parts);
		}

		/// <summary>
		/// Reads the message header of a mail message and returns it as a
		/// NameValueCollection.
		/// </summary>
		/// <returns>A NameValueCollection containing the header fields as keys
		/// with their respective values as values.</returns>
		private NameValueCollection ReadMailHeader() {
			NameValueCollection header = new NameValueCollection();
			string response, fieldname = null, fieldvalue = null;
			while ((response = GetResponse()) != String.Empty) {
				/* Values may stretch over several lines */
				if (response[0] == ' ' || response[0] == '\t') {
					header[fieldname] = header[fieldname] +
						response.Substring(1).Trim();
					continue;
				}
				/* The mail header consists of field:value pairs */
				int delimiter = response.IndexOf(':');
				fieldname = response.Substring(0, delimiter).Trim();
				fieldvalue = response.Substring(delimiter + 1).Trim();
				header.Add(fieldname, fieldvalue);
			}
			return header;
		}

		/// <summary>
		/// Parses a MIME header field which can contain multiple 'parameter = value'
		/// pairs (such as Content-Type: text/html; charset=iso-8859-1).
		/// </summary>
		/// <param name="field">The header field to parse</param>
		/// <returns>A NameValueCollection containing the parameter names as keys
		/// with the respective parameter values as values.</returns>
		/// <remarks>The value of the actual field disregarding the 'parameter = value'
		/// pairs is stored in the collection under the key "value" (in the above example
		/// of Content-Type, this would be "text/html").</remarks>
		private NameValueCollection ParseMIMEField(string field) {
			NameValueCollection coll = new NameValueCollection();
			MatchCollection matches = Regex.Matches(field, @"([\w\-]+)=\W*([\w\-\/\.]+)");
			foreach (Match m in matches)
				coll.Add(m.Groups[1].Value, m.Groups[2].Value);
			Match mvalue = Regex.Match(field, @"^\s*([\w\/]+)");
			coll.Add("value", mvalue.Success ? mvalue.Groups[1].Value : "");
			return coll;
		}

		/// <summary>
		/// Parses a mail header address-list field such as To, Cc and Bcc which
		/// can contain multiple email addresses.
		/// </summary>
		/// <param name="list">The address-list field to parse</param>
		/// <returns>An array of strings containing the parsed mail
		/// addresses.</returns>
		private string[] ParseAddressList(string list) {
			List<string> mails = new List<string>();
			MatchCollection matches = Regex.Matches(list,
				@"\b([A-Z0-9._%-]+@[A-Z0-9.-]+\.[A-Z]{2,4})\b", RegexOptions.IgnoreCase);
			foreach (Match m in matches)
				mails.Add(m.Groups[1].Value);
			return mails.ToArray();
		}

		/// <summary>
		/// Parses a mail message identifier from a string.
		/// </summary>
		/// <param name="field">The field to parse the message id from</param>
		/// <exception cref="ArgumentException">Thrown when the field
		/// argument does not contain a valid message identifier.</exception>
		/// <returns>The parsed message id</returns>
		/// <remarks>A message identifier (msg-id) is a globally unique
		/// identifier for a message.</remarks>
		private string ParseMessageId(string field) {
			/* a msg-id is enclosed in < > brackets */
			Match m = Regex.Match(field, @"<(.+)>");
			if (m.Success)
				return m.Groups[1].Value;
			throw new ArgumentException("The field does not contain a valid message " +
				"identifier: " + field);
		}

		/// <summary>
		/// Reads the plain-text message body of a mail message.
		/// </summary>
		/// <returns>The message body of the mail message.</returns>
		private string ReadMailBody() {
			string response, body = "";
			while ((response = GetResponse()) != ")")
				body = body + response + "\r\n";
			return body;
		}

		/// <summary>
		/// Reads the message body of a MIME multipart message.
		/// </summary>
		/// <param name="boundary">The boundary string which separates
		/// the different parts which make up the multipart-message</param>
		/// <param name="nested">True if recursive call</param>
		/// <returns>A list of the MIME parts composing the multipart
		/// message</returns>
		/// <remarks>Each MIME part consists of its own set of header
		/// fields and a body.</remarks>
		private MIMEPart[] ReadMultipartBody(string boundary, bool nested = false) {
			List<MIMEPart> parts = new List<MIMEPart>();
			string s_boundary = "--" + boundary,
				e_boundary = "--" + boundary + "--";
			/* skip everything up to the first boundary */
			string response = GetResponse();
			while (!response.StartsWith(s_boundary))
				response = GetResponse();
			/* read MIME parts enclosed in boundary strings */
			while (response.StartsWith(s_boundary)) {
				MIMEPart part = new MIMEPart();
				/* read content-header of part */
				part.header = ReadMailHeader();
				/* account for nested multipart content */
				NameValueCollection contentType = ParseMIMEField(
					part.header["Content-Type"]);
				if (contentType["Boundary"] != null)
					parts.AddRange(ReadMultipartBody(contentType["boundary"], true));
				/* read content-body of part */
				while (!(response = GetResponse()).StartsWith(s_boundary))
					part.body = part.body + response + "\r\n";
				/* add MIME part to the list unless body is null which means the body was
						nested multipart content */
				if(part.body != null)
					parts.Add(part);
				/* if the boundary is actually the end boundary, we're done */
				if (response.StartsWith(e_boundary))
					break;
			}
			if (nested == false) {
				/* FETCH closing bracket may be last character of response */
				if (!response.EndsWith(")")) {
					/* next read should return closing bracket from FETCH command then */
					if ((response = GetResponse()) != ")")
						throw new BadServerResponseException(response);
				}
			}
			return parts.ToArray();
		}

		/// <summary>
		/// Creates a new instance of the MailMessage class and initializes it using
		/// the specified header and body information.
		/// </summary>
		/// <param name="header">A collection of mail and MIME headers</param>
		/// <param name="body">The mail body. May be null in case the message
		/// is a MIME multi-part message in which case the MailMessage's body will
		/// be set to the body of the first MIME part.</param>
		/// <param name="parts">An array of MIME parts making up the message. If the
		/// message is not a MIME multi-part message, this can be set to null.
		/// </param>
		/// <returns>An initialized instance of the MailMessage class</returns>
		private MailMessage CreateMailmessage(NameValueCollection header, string body,
			MIMEPart[] parts) {
			MailMessage m = new MailMessage();
			NameValueCollection contentType = ParseMIMEField(
				header["Content-Type"]);
			/* NameValueCollection throws an exception if adding an empty string as
			 * value which can happen, if reading a mail message with an empty subject
			 * for instance
			 */
			foreach (string key in header) {
				string value = header.GetValues(key)[0];
				if (value != String.Empty)
					m.Headers.Add(key, value);
			}
			if (parts != null) {
				/* This takes care of setting the Body, BodyEncoding and IsBodyHtml fields also */
				AddMIMEPartsToMessage(m, parts);
			} else {
				/* charset attribute should be part of content-type */
				try {
					m.BodyEncoding = Encoding.GetEncoding(
							contentType["charset"]);
				} catch {
					m.BodyEncoding = Encoding.ASCII;
				}
				m.Body = body;
				m.IsBodyHtml = contentType["value"].Contains("text/html");
			}
			Match ma = Regex.Match(header["Subject"], @"=\?([A-Za-z0-9\-]+)");
			if (ma.Success) {
				/* encoded-word subject */
				m.SubjectEncoding = Encoding.GetEncoding(
					ma.Groups[1].Value);
				m.Subject = Util.DecodeWords(header["Subject"]);
			} else {
				m.SubjectEncoding = Encoding.ASCII;
				m.Subject = header["Subject"];
			}
			m.Priority = header["Priority"] != null ?
				PriorityMapping[header["Priority"]] : MailPriority.Normal;			
			SetAddressFields(m, header);
			return m;
		}

		/// <summary>
		/// A mapping to map MIME priority values to their MailPriority enum
		/// counterparts.
		/// </summary>
		static private Dictionary<string, MailPriority> PriorityMapping =
			new Dictionary<string, MailPriority>(StringComparer.OrdinalIgnoreCase) {
				{ "non-urgent", MailPriority.Low },
				{ "normal",	MailPriority.Normal },
				{ "urgent",	MailPriority.High }
			};

		/// <summary>
		/// Sets the address fields (From, To, CC, etc.) of a MailMessage
		/// object using the specified mail message header information.
		/// </summary>
		/// <param name="m">The MailMessage instance to operate on</param>
		/// <param name="header">A collection of mail and MIME headers</param>
		private void SetAddressFields(MailMessage m, NameValueCollection header) {
			string[] addr = ParseAddressList(header["To"]);
			foreach (string s in addr)
				m.To.Add(s);
			if (header["Cc"] != null) {
				addr = ParseAddressList(header["Cc"]);
				foreach (string s in addr)
					m.CC.Add(s);
			}
			if (header["Bcc"] != null) {
				addr = ParseAddressList(header["Bcc"]);
				foreach (string s in addr)
					m.Bcc.Add(s);
			}
			if (header["From"] != null) {
				addr = ParseAddressList(header["From"]);
				m.From = new MailAddress(addr.Length > 0 ? addr[0] : "");
			}
			if (header["Sender"] != null) {
				addr = ParseAddressList(header["Sender"]);
				m.Sender = new MailAddress(addr.Length > 0 ? addr[0] : "");
			}
			if (header["Reply-to"] != null) {
				addr = ParseAddressList(header["Reply-to"]);
				foreach (string s in addr)
					m.ReplyToList.Add(s);
			}
		}

		/// <summary>
		/// Adds the parts of a MIME multi-part message to an instance of the
		/// MailMessage class. MIME parts are either added to the AlternateViews
		/// or to the Attachments collections depending on their type.
		/// </summary>
		/// <param name="m">The MailMessage instance to operate on</param>
		/// <param name="parts">An array of MIME parts</param>
		private void AddMIMEPartsToMessage(MailMessage m, MIMEPart[] parts) {
			bool setBodyFields = false;
			for (int i = 0; i < parts.Length; i++) {
				MIMEPart p = parts[i];
				NameValueCollection contentType = ParseMIMEField(
					p.header["Content-Type"]);
				string transferEnc = p.header["Content-Transfer-Encoding"] ??
					"none";
				Encoding encoding = Encoding.GetEncoding(
					contentType["Charset"] ?? "us-ascii");
				byte[] bytes = encoding.GetBytes(p.body);
				/* decode content if it was encoded */
				switch (transferEnc.ToLower()) {
					case "quoted-printable":
						bytes = encoding.GetBytes(Util.QPDecode(p.body, encoding));
						break;
					case "base64":
						bytes = Util.Base64Decode(p.body);
						break;
				}
				/* Put the first MIME part that contains text into the Body fields of the
				 * MailMessage instance */
				if (setBodyFields == false && contentType["value"].ToLower().Contains("text")) {
						m.Body = encoding.GetString(bytes);
						m.BodyEncoding = encoding;
						m.IsBodyHtml = contentType["value"].ToLower()
							.Contains("text/html");
						setBodyFields = true;
						continue;
				}
				NameValueCollection disposition = ParseMIMEField(
					p.header["Content-Disposition"] ?? "");
				if (disposition["value"].ToLower() == "attachment") {
					m.Attachments.Add(CreateAttachment(p.header, bytes,
						disposition["filename"] ?? ("attachment" + i.ToString())));
				} else {
					m.AlternateViews.Add(CreateAlternateView(p.header, bytes));
				}
			}
		}

		/// <summary>
		/// Creates an instance of the Attachment class used by the MailMessage class
		/// to store mail message attachments.
		/// </summary>
		/// <param name="header">The MIME part header</param>
		/// <param name="bytes">An array of bytes composing the content of the
		/// attachment</param>
		/// <param name="name">The designated name of the attachment file</param>
		/// <returns>An initialized instance of the Attachment class</returns>
		private Attachment CreateAttachment(NameValueCollection header, byte[] bytes, string name) {
			MemoryStream stream = new MemoryStream(bytes);
			Attachment attachment = new Attachment(stream, name);
			try {
				attachment.ContentId = ParseMessageId(header["Content-Id"]);
			} catch {
			}
			attachment.ContentType = new System.Net.Mime.ContentType(header["Content-Type"]);
			return attachment;
		}

		/// <summary>
		/// Creates an instance of the AlternateView class used by the MailMessage class
		/// to store alternate views of the mail message's content.
		/// </summary>
		/// <param name="header">The MIME part header</param>
		/// <param name="bytes">An array of bytes composing the content of the
		/// alternate view</param>
		/// <returns>An initialized instance of the AlternateView class</returns>
		private AlternateView CreateAlternateView(NameValueCollection header, byte[] bytes) {
			MemoryStream stream = new MemoryStream(bytes);
			AlternateView view = new AlternateView(stream,
				new System.Net.Mime.ContentType(header["Content-Type"]));
			try {
				view.ContentId = ParseMessageId(header["Content-Id"]);
			} catch {
			}
			view.ContentType = new System.Net.Mime.ContentType(header["Content-Type"]);
			return view;
		}
	}
}
