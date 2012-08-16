using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace S22.Imap {
	/// <summary>
	/// Allows applications to communicate with a mail server by using the
	/// Internet Message Access Protocol (IMAP).
	/// </summary>
	public class ImapClient : IDisposable {
		private Stream stream;
		private TcpClient client;
		private readonly object readLock = new object();
		private readonly object writeLock = new object();
		private string[] capabilities;
		private int tag = 0;
		private string selectedMailbox;
		private string defaultMailbox = "INBOX";
		private event EventHandler<IdleMessageEventArgs> newMessageEvent;
		private event EventHandler<IdleMessageEventArgs> messageDeleteEvent;
		private bool hasEvents {
			get {
				return newMessageEvent != null || messageDeleteEvent != null;
			}
		}
		private bool idling;
		private Thread idleThread;

		/// <summary>
		/// The default mailbox to operate on, when no specific mailbox name was indicated
		/// to methods operating on mailboxes. This property is initially set to "INBOX".
		/// </summary>
		/// <exception cref="ArgumentNullException">The value specified for a set operation is
		/// null.</exception>
		/// <exception cref="ArgumentException">The value specified for a set operation is equal
		/// to String.Empty ("").</exception>
		/// <remarks>This property is initialized to "INBOX"</remarks>
		public string DefaultMailbox {
			get {
				return defaultMailbox;
			}
			set {
				if (value == null)
					throw new ArgumentNullException();
				if (value == String.Empty)
					throw new ArgumentException();
				defaultMailbox = value;
			}
		}

		/// <summary>
		/// Indicates whether the client is authenticated with the server
		/// </summary>
		public bool Authed {
			get;
			private set;
		}

		/// <summary>
		/// This event is raised when a new mail message is received by the server.
		/// </summary>
		/// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
		/// method can be used, specifying "IDLE" for the capability parameter.
		/// 
		/// Notice that the event handler will be executed on a threadpool thread.
		/// </remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="NewMessage"]/*'/>
		public event EventHandler<IdleMessageEventArgs> NewMessage {
			add {
				newMessageEvent += value;
				StartIdling();
			}
			remove {
				newMessageEvent -= value;
				if (!hasEvents)
					StopIdling();
			}
		}

		/// <summary>
		/// This event is raised when a message is deleted on the server.
		/// </summary>
		/// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
		/// method can be used, specifying "IDLE" for the capability parameter.
		/// 
		/// Notice that the event handler will be executed on a threadpool thread.
		/// </remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="MessageDeleted"]/*'/>
		public event EventHandler<IdleMessageEventArgs> MessageDeleted {
			add {
				messageDeleteEvent += value;
				StartIdling();
			}
			remove {
				messageDeleteEvent -= value;
				if (!hasEvents)
					StopIdling();
			}
		}

		/// <summary>
		/// Initializes a new instance of the ImapClient class and connects to the specified port
		/// on the specified host, optionally using the Secure Socket Layer (SSL) security protocol.
		/// </summary>
		/// <param name="hostname">The DNS name of the server to which you intend to connect.</param>
		/// <param name="port">The port number of the server to which you intend to connect.</param>
		/// <param name="ssl">Set to true to use the Secure Socket Layer (SSL) security protocol.</param>
		/// <param name="validate">Delegate used for verifying the remote Secure Sockets
		/// Layer (SSL) certificate which is used for authentication. Set this to null if not needed</param>
		/// <exception cref="ArgumentOutOfRangeException">The port parameter is not between MinPort
		/// and MaxPort.</exception>
		/// <exception cref="ArgumentNullException">The hostname parameter is null.</exception>
		/// <exception cref="SocketException">An error occurred while accessing the socket used for
		/// establishing the connection to the IMAP server. Use the ErrorCode property to obtain the
		/// specific error code</exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">An authentication
		/// error occured while trying to establish a secure connection.</exception>
		/// <exception cref="BadServerResponseException">Thrown if an unexpected response is received
		/// from the server upon connecting.</exception>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="ctor-1"]/*'/>
		public ImapClient(string hostname, int port = 143, bool ssl = false,
			RemoteCertificateValidationCallback validate = null) {
			Connect(hostname, port, ssl, validate);
		}

		/// <summary>
		/// Initializes a new instance of the ImapClient class and connects to the specified port on
		/// the specified host, optionally using the Secure Socket Layer (SSL) security protocol and
		/// attempts to authenticate with the server using the specified authentication method and
		/// credentials.
		/// </summary>
		/// <param name="hostname">The DNS name of the server to which you intend to connect.</param>
		/// <param name="port">The port number of the server to which you intend to connect.</param>
		/// <param name="username">The username with which to login in to the IMAP server.</param>
		/// <param name="password">The password with which to log in to the IMAP server.</param>
		/// <param name="method">The requested method of authentication. Can be one of the values
		/// of the AuthMethod enumeration.</param>
		/// <param name="ssl">Set to true to use the Secure Socket Layer (SSL) security protocol.</param>
		/// <param name="validate">Delegate used for verifying the remote Secure Sockets Layer
		/// (SSL) certificate which is used for authentication. Set this to null if not needed</param>
		/// <exception cref="ArgumentOutOfRangeException">The port parameter is not between MinPort
		/// and MaxPort.</exception>
		/// <exception cref="ArgumentNullException">The hostname parameter is null.</exception>
		/// <exception cref="SocketException">An error occurred while accessing the socket used for
		/// establishing the connection to the IMAP server. Use the ErrorCode property to obtain the
		/// specific error code</exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">An authentication
		/// error occured while trying to establish a secure connection.</exception>
		/// <exception cref="BadServerResponseException">Thrown if an unexpected response is received
		/// from the server upon connecting.</exception> 
		/// <exception cref="InvalidCredentialsException">Thrown if authentication using the
		/// supplied credentials failed.</exception>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="ctor-2"]/*'/>
		public ImapClient(string hostname, int port, string username, string password, AuthMethod method =
			AuthMethod.Login, bool ssl = false, RemoteCertificateValidationCallback validate = null) {
			Connect(hostname, port, ssl, validate);
			Login(username, password, method);
		}

		/// <summary>
		/// Connects to the specified port on the specified host, optionally using the Secure Socket Layer
		/// (SSL) security protocol.
		/// </summary>
		/// <param name="hostname">The DNS name of the server to which you intend to connect.</param>
		/// <param name="port">The port number of the server to which you intend to connect.</param>
		/// <param name="ssl">Set to true to use the Secure Socket Layer (SSL) security protocol.</param>
		/// <param name="validate">Delegate used for verifying the remote Secure Sockets
		/// Layer (SSL) certificate which is used for authentication. Set this to null if not needed</param>
		/// <exception cref="ArgumentOutOfRangeException">The port parameter is not between MinPort
		/// and MaxPort.</exception>
		/// <exception cref="ArgumentNullException">The hostname parameter is null.</exception>
		/// <exception cref="SocketException">An error occurred while accessing the socket used for
		/// establishing the connection to the IMAP server. Use the ErrorCode property to obtain the
		/// specific error code.</exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">An authentication
		/// error occured while trying to establish a secure connection.</exception>
		/// <exception cref="BadServerResponseException">Thrown if an unexpected response is received
		/// from the server upon connecting.</exception>
		private void Connect(string hostname, int port, bool ssl, RemoteCertificateValidationCallback validate) {
			client = new TcpClient(hostname, port);
			stream = client.GetStream();
			if (ssl) {
				SslStream sslStream = new SslStream(stream, false, validate ??
					((sender, cert, chain, err) => true));
				sslStream.AuthenticateAsClient(hostname);
				stream = sslStream;
			}
			/* Server issues untagged OK greeting upon connect */
			string greeting = GetResponse();			
			if (!IsResponseOK(greeting))
				throw new BadServerResponseException(greeting);
		}

		/// <summary>
		/// Determines whether the received response is a valid IMAP OK response.
		/// </summary>
		/// <param name="response">A response string received from the server</param>
		/// <param name="tag">A tag if the response is associated with a command</param>
		/// <returns>True if the response is a valid IMAP OK response, otherwise false
		/// is returned.</returns>
		private bool IsResponseOK(string response, string tag = null) {
			if (tag != null)
				return response.StartsWith(tag + "OK");
			string v = response.Substring(response.IndexOf(' ')).Trim();
			return v.StartsWith("OK");
		}

		/// <summary>
		/// Attempts to establish an authenticated session with the server using the specified
		/// credentials.
		/// </summary>
		/// <param name="username">The username with which to login in to the IMAP server.</param>
		/// <param name="password">The password with which to log in to the IMAP server.</param>
		/// <param name="method">The requested method of authentication. Can be one of the values
		/// of the AuthMethod enumeration.</param>
		/// <exception cref="InvalidCredentialsException">Thrown if authentication using the
		/// supplied credentials failed.</exception>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Login"]/*'/>
		public void Login(string username, string password, AuthMethod method) {
			string tag = GetTag();
			string response = null;
			switch (method) {
				case AuthMethod.Login:
					response = SendCommandGetResponse(tag + "LOGIN " + username.QuoteString() + " " +
						password.QuoteString());
					break;
				case AuthMethod.CRAMMD5:
          response = SendCommandGetResponse(tag + "AUTHENTICATE CRAM-MD5");
          /* retrieve server key */
          string key = Encoding.Default.GetString(
						Convert.FromBase64String(response.Replace("+ ", "")));
					/* compute the hash */
          using (var kMd5 = new HMACMD5(Encoding.ASCII.GetBytes(password))) {
            byte[] hash1 = kMd5.ComputeHash(Encoding.ASCII.GetBytes(key));
            key = BitConverter.ToString(hash1).ToLower().Replace("-", "");
            string command = Convert.ToBase64String(
							Encoding.ASCII.GetBytes(username + " " + key));
            response = SendCommandGetResponse(command);
          }
					break;
				case AuthMethod.SaslOAuth:
					response = SendCommandGetResponse(tag + "AUTHENTICATE XOAUTH " + password);
					break;
			}
			/* Server may include a CAPABILITY response */
			if (response.StartsWith("* CAPABILITY")) {
				capabilities = response.Substring(13).Trim().Split(' ');
				response = GetResponse();
			}

			if (!IsResponseOK(response, tag))
				throw new InvalidCredentialsException(response);
			Authed = true;
		}

		/// <summary>
		/// Logs an authenticated client out of the server. After the logout sequence has
		/// been completed, the server closes the connection with the client.
		/// </summary>
		/// <exception cref="BadServerResponseException">Thrown if an unexpected response is
		/// received from the server during the logout sequence</exception>
		/// <remarks>Calling Logout in a non-authenticated state has no effect</remarks>
		public void Logout() {
			if (!Authed)
				return;
			StopIdling();
			string tag = GetTag();
			string bye = SendCommandGetResponse(tag + "LOGOUT");
			if (!bye.StartsWith("* BYE"))
				throw new BadServerResponseException(bye);
			string response = GetResponse();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
			Authed = false;
		}

		/// <summary>
		/// Generates a unique identifier to prefix a command with, as is
		/// required by the IMAP protocol.
		/// </summary>
		/// <returns>A unique identifier string</returns>
		private string GetTag() {
			Interlocked.Increment(ref tag);
			return string.Format("xm{0:000} ", tag);
		}

		/// <summary>
		/// Sends a command string to the server. This method blocks until the command has
		/// been transmitted.
		/// </summary>
		/// <param name="command">Command string to be sent to the server. The command string is
		/// suffixed by CRLF (as is required by the IMAP protocol) prior to sending.</param>
		private void SendCommand(string command) {
			byte[] bytes = Encoding.ASCII.GetBytes(command + "\r\n");
			lock (writeLock) {
				stream.Write(bytes, 0, bytes.Length);
			}
		}

		/// <summary>
		/// Sends a command string to the server and subsequently waits for a response, which is
		/// then returned to the caller. This method blocks until the server response has been
		/// received.
		/// </summary>
		/// <param name="command">Command string to be sent to the server. The command string is
		/// suffixed by CRLF (as is required by the IMAP protocol) prior to sending.</param>
		/// <returns>The response received by the server.</returns>
		private string SendCommandGetResponse(string command) {
			lock (readLock) {
				lock (writeLock) {
					SendCommand(command);
				}
				return GetResponse();
			}
		}

		/// <summary>
		/// Waits for a response from the server. This method blocks
		/// until a response has been received.
		/// </summary>
		/// <returns>A response string from the server</returns>
		private string GetResponse() {
			const int Newline = 10, CarriageReturn = 13;
			using (var mem = new MemoryStream()) {
				lock (readLock) {
					while (true) {
						byte b = (byte)stream.ReadByte();
						if (b == CarriageReturn)
							continue;
						if (b == Newline) {
							return Encoding.ASCII.GetString(mem.ToArray());
						} else
							mem.WriteByte(b);
					}
				}
			}
		}

		/// <summary>
		/// Returns a listing of capabilities that the IMAP server supports. All strings
		/// in the returned array are guaranteed to be upper-case.
		/// </summary>
		/// <exception cref="BadServerResponseException">Thrown if an unexpected response is received
		/// from the server during the request. The message property of the exception contains the
		/// error message returned by the server.</exception>
		/// <returns>A listing of supported capabilities as an array of strings</returns>
		public string[] Capabilities() {
			if (capabilities != null)
				return capabilities;
			PauseIdling();
			string tag = GetTag();
			string command = tag + "CAPABILITY";
			string response = SendCommandGetResponse(command);
			/* Server is required to issue untagged capability response */
			if (response.StartsWith("* CAPABILITY "))
				response = response.Substring(13);
			capabilities = response.Trim().Split(' ');
			/* should return OK */
			response = GetResponse();
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
			return capabilities;
		}

		/// <summary>
		/// Returns whether the specified capability is supported by the server.
		/// </summary>
		/// <param name="capability">The capability to probe for (for example "IDLE")</param>
		/// <exception cref="BadServerResponseException">Thrown if an unexpected response is received
		/// from the server during the request. The message property of the exception contains
		/// the error message returned by the server.</exception>
		/// <returns>Returns true if the specified capability is supported by the server, 
		/// otherwise false is returned.</returns>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="ctor-2"]/*'/>
		public bool Supports(string capability) {
			return (capabilities ?? Capabilities()).Contains(capability.ToUpper());
		}

		/// <summary>
		/// Changes the name of a mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to rename.</param>
		/// <param name="newName">The new name the mailbox will be renamed to.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mailbox could
		/// not be renamed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		public void RenameMailbox(string mailbox, string newName) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "RENAME " +
				mailbox.QuoteString() + " " + newName.QuoteString());
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
		}

		/// <summary>
		/// Permanently removes a mailbox.
		/// </summary>
		/// <param name="mailbox">Name of the mailbox to remove.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mailbox could
		/// not be removed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		public void DeleteMailbox(string mailbox) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "DELETE " +
				mailbox.QuoteString());
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
		}

		/// <summary>
		/// Creates a new mailbox with the given name.
		/// </summary>
		/// <param name="mailbox">Name of the mailbox to create.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mailbox could
		/// not be created. The message property of the exception contains the error message
		/// returned by the server.</exception>
		public void CreateMailbox(string mailbox) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "CREATE " +
				mailbox.QuoteString());
			ResumeIdling();
			if(!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
		}

		/// <summary>
		/// Selects a mailbox so that messages in the mailbox can be accessed.
		/// </summary>
		/// <param name="mailbox">The mailbox to select. If this parameter is null, the
		/// default mailbox is selected.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mailbox could
		/// not be selected. The message property of the exception contains the error message
		/// returned by the server.</exception>
		private void SelectMailbox(string mailbox) {
			if (!Authed)
				throw new NotAuthenticatedException();
			if (mailbox == null)
				mailbox = defaultMailbox;
			/* requested mailbox is already selected */
			if (selectedMailbox == mailbox)
				return;
			PauseIdling();
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "SELECT " +
				mailbox.QuoteString());
			/* evaluate untagged data */
			while (response.StartsWith("*")) {
				// Fixme: evaluate data
				response = GetResponse();
			}
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
			selectedMailbox = mailbox;
			ResumeIdling();
		}

		/// <summary>
		/// Retrieves a list of all available and selectable mailboxes on the server.
		/// </summary>
		/// <returns>A list of all available and selectable mailboxes</returns>
		/// <remarks>The mailbox INBOX is a special name reserved to mean "the
		/// primary mailbox for this user on this server"</remarks>
		public string[] ListMailboxes() {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			List<string> mailboxes = new List<string>();
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "LIST \"\" \"*\"");
			while (response.StartsWith("*")) {
				Match m = Regex.Match(response,
					"\\* LIST \\((.*)\\)\\s+\"(.+)\"\\s+\"(.+)\"");
				if (!m.Success)
					continue;
				string[] attr = m.Groups[1].Value.Split(new char[] { ' ' });
				foreach (string a in attr) {
					/* Only list selectable mailboxes */
					if (a.ToLower() == @"\noselect")
						continue;
				}
				mailboxes.Add(m.Groups[3].Value);
				response = GetResponse();
			}
			ResumeIdling();
			return mailboxes.ToArray();
		}

		/// <summary>
		/// Permanently removes all messages that have the \Deleted flag set from the
		/// specified mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to remove all messages from that have the
		/// \Deleted flag set. If this parameter is omitted, the value of the DefaultMailbox
		/// property is used to determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the expunge operation could
		/// not be completed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		public void Expunge(string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			SelectMailbox(mailbox);
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "EXPUNGE");
			/* Server is required to send an untagged response for each message that is
			 * deleted before sending OK */
			while (response.StartsWith("*"))
				response = GetResponse();
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
		}

		/// <summary>
		/// Retrieves status information (total number of messages, number of unread
		/// messages, etc.) for the specified mailbox. </summary>
		/// <param name="mailbox">The mailbox to retrieve status information for. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <returns>A MailboxStatus object containing status information for the
		/// mailbox.</returns>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the operation could
		/// not be completed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetStatus"]/*'/>
		public MailboxStatus GetStatus(string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			if (mailbox == null)
				mailbox = defaultMailbox;
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "STATUS " +
				mailbox.QuoteString() + " (MESSAGES UNSEEN)");
			int messages = 0, unread = 0;
			while (response.StartsWith("*")) {
				Match m = Regex.Match(response, @"\* STATUS.*MESSAGES (\d+)");
				if (m.Success)
					messages = Convert.ToInt32(m.Groups[1].Value);
				m = Regex.Match(response, @"\* STATUS.*UNSEEN (\d+)");
				if (m.Success)
					unread = Convert.ToInt32(m.Groups[1].Value);
				response = GetResponse();
			}
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
			return new MailboxStatus(messages, unread);
		}

		/// <summary>
		/// Searches the specified mailbox for messages that match the given
		/// searching criteria.
		/// </summary>
		/// <param name="criteria">A search criteria expression. Only messages
		/// that match this expression will be included in the result set returned
		/// by this method.</param>
		/// <param name="mailbox">The mailbox that will be searched. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox
		/// to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the search could
		/// not be completed. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <returns>An array of unique identifier (UID) message attributes which
		/// can be used with the GetMessage family of methods to download mail
		/// messages.</returns>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each
		/// message which uniquely identifies the message. No two messages share the
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Search"]/*'/>
		public uint[] Search(SearchCondition criteria, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			SelectMailbox(mailbox);
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "UID SEARCH " +
				criteria.ToString());
			List<uint> result = new List<uint>();
			while (response.StartsWith("*")) {
				Match m = Regex.Match(response, @"^\* SEARCH (.*)");
				if (m.Success) {
					string[] v = m.Groups[1].Value.Trim().Split(' ');
					foreach (string s in v)
						result.Add(Convert.ToUInt32(s));
				}
				response = GetResponse();
			}
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
			return result.ToArray();
		}

		/// <summary>
		/// Retrieves a mail message by its unique identifier message attribute.
		/// </summary>
		/// <param name="uid">The unique identifier of the mail message to retrieve</param>
		/// <param name="seen">Set this to true to set the \Seen flag for this message
		/// on the server.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message could
		/// not be fetched. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>An initialized instance of the MailMessage class representing the
		/// fetched mail message</returns>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each
		/// message which uniquely identifies the message. No two messages share the
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage"]/*'/>
		public MailMessage GetMessage(uint uid, bool seen = true, string mailbox = null) {
			MailMessage[] M = GetMessages(new uint[] { uid }, seen, mailbox);

			return M[0];
		}

		/// <summary>
		/// Retrieves a set of mail messages by their unique identifier message attributes.
		/// </summary>
		/// <param name="uids">An array of unique identifiers of the mail messages to
		/// retrieve</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched
		/// messages on the server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail messages could
		/// not be fetched. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>An array of initialized instances of the MailMessage class representing
		/// the fetched mail messages</returns>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each
		/// message which uniquely identifies the message. No two messages share the
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages"]/*'/>
		public MailMessage[] GetMessages(uint[] uids, bool seen = true, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			PauseIdling();
			SelectMailbox(mailbox);
			List<MailMessage> messages = new List<MailMessage>();
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "UID FETCH " +
				string.Join<uint>(",", uids) + " (BODY" + (seen ? null : ".PEEK") + "[])");

			/* ready any untagged responses */
			while (response.StartsWith("*")) {
				Match m = Regex.Match(response, @"\* (\d+) FETCH");
				if (!m.Success)
					throw new BadServerResponseException(response);
				uint uid = Convert.ToUInt32(m.Groups[1].Value);
				/* fetch the actual message header and data */
				messages.Add(new MessageReader(GetResponse).
					ReadMailMessage(uid));
				response = GetResponse();
			}
			ResumeIdling();
			if (!IsResponseOK(response, tag))
				throw new BadServerResponseException(response);
			return messages.ToArray();
		}

		/// <summary>
		/// Starts receiving of IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the server does
		/// not support the IMAP4 IDLE command.</exception>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the IDLE operation could
		/// not be completed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ApplicationException">Thrown if an unexpected program condition
		/// occured.</exception>
		/// <remarks>Calling this method when already receiving idle notifications
		/// has no effect.</remarks>
		/// <seealso cref="StopIdling"/>
		/// <seealso cref="PauseIdling"/>
		/// <seealso cref="ResumeIdling"/>
		private void StartIdling() {
			if (idling)
				return;
			idling = true;
			ResumeIdling();
		}

		/// <summary>
		/// Stops receiving of IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the server does
		/// not support the IMAP4 IDLE command.</exception>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the IDLE operation could
		/// not be completed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <remarks>Calling this method when not receiving idle notifications
		/// has no effect.</remarks>
		/// <seealso cref="StartIdling"/>
		/// <seealso cref="PauseIdling"/>
		private void StopIdling() {
			PauseIdling();
			idling = false;
		}

		/// <summary>
		/// Temporarily pauses receiving of IMAP IDLE notifications from the IMAP
		/// server.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the server does
		/// not support the IMAP4 IDLE command.</exception>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the IDLE operation could
		/// not be completed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <remarks>To resume receiving IDLE notifications ResumeIdling must be called
		/// </remarks>
		/// <seealso cref="StartIdling"/>
		/// <seealso cref="ResumeIdling"/>
		private void PauseIdling() {
			if (!Authed)
				throw new NotAuthenticatedException();
			if (!idling)
				return;
			if (!Supports("IDLE"))
				throw new InvalidOperationException("The server does not support the " +
					"IMAP4 IDLE command");
			/* Send server "DONE" continuation-command to indicate we no longer wish
			 * to receive idle notifications. The server response is consumed by
			 * the idle thread and signals it to shut down.
			 */
			SendCommand("DONE");

			/* Wait until idle thread has shutdown */
			idleThread.Join();
			idleThread = null;
		}

		/// <summary>
		/// Resumes receiving of IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the server does
		/// not support the IMAP4 IDLE command.</exception>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the IDLE operation could
		/// not be completed. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ApplicationException">Thrown if an unexpected program condition
		/// occured.</exception>
		/// <remarks>This method is usually called in response to a prior call to the
		/// PauseIdling method.</remarks>
		/// <seealso cref="StopIdling"/>
		private void ResumeIdling() {
			if (!Authed)
				throw new NotAuthenticatedException();
			if (!idling)
				return;
			if (!Supports("IDLE"))
				throw new InvalidOperationException("The server does not support the " +
					"IMAP4 IDLE command");
			/* Make sure a mailbox is selected */
			SelectMailbox(null);
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "IDLE");
			/* Server must respond with a '+' continuation response */
			if (!response.StartsWith("+"))
				throw new BadServerResponseException(response);
			/* setup and start the idle thread */
			if (idleThread != null)
				throw new ApplicationException("idleThread is not null");
			idleThread = new Thread(IdleLoop);
			idleThread.Start();
		}

		/// <summary>
		/// The main idle loop. Waits for incoming IMAP IDLE notifications and dispatches
		/// them as events. This runs in its own thread whenever IMAP IDLE
		/// notifications are to be received.
		/// </summary>
		private void IdleLoop() {
			while (true) {
				string response = WaitForResponse();
				/* A request was made to stop idling so quit the thread */
				if (response.Contains("OK IDLE"))
					return;
				Match m = Regex.Match(response, @"\*\s+(\d+)\s+(\w+)");
				if (!m.Success)
					continue;
				/* Examine the notification */
				uint numberOfMessages = Convert.ToUInt32(m.Groups[1].Value);
				uint highestUID = 0;

				switch (m.Groups[2].Value.ToUpper()) {
					case "EXISTS":
						ThreadPool.QueueUserWorkItem(callback => {
							newMessageEvent.Raise(this,
								new IdleMessageEventArgs(numberOfMessages, highestUID, this));
						});
						break;
					case "EXPUNGE":
						ThreadPool.QueueUserWorkItem(callback => messageDeleteEvent.Raise(
							this, new IdleMessageEventArgs(numberOfMessages, highestUID, this)));
						break;
				}
			}
		}

		/// <summary>
		/// Blocks until an IMAP notification has been received while taking
		/// care of issuing NOOP's to the IMAP server at regular intervals
		/// </summary>
		/// <returns>The IMAP command received from the server</returns>
		private string WaitForResponse() {
			string response = null;
			int noopInterval = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
			AutoResetEvent ev = new AutoResetEvent(false);

			ThreadPool.QueueUserWorkItem(_ => {
				try {
					response = GetResponse();
					ev.Set();
				} catch (IOException) {
					/* Closing _Stream or the underlying _Connection instance will
					 * cause a WSACancelBlockingCall exception on a blocking socket.
					 * This is not an error so just let it pass.
					 */
				}
			});
			if (ev.WaitOne(noopInterval))
				return response;
			/* Still here means the NOOP timeout was hit. WorkItem thread is still
			 * in a blocking read which _must_ be consumed.
			 */
			SendCommand("DONE");
			ev.WaitOne();
			if (response.Contains("OK IDLE") == false) {
				/* Shouldn't happen really */
			}
			/* Perform actual NOOP command and resume idling afterwards */
			IssueNoop();
			response = SendCommandGetResponse(GetTag() + "IDLE");
			if (!response.StartsWith("+"))
				throw new BadServerResponseException(response);
			/* Go back to receiving IDLE notifications */
			return WaitForResponse();
		}

		/// <summary>
		/// Issues a NOOP command to the IMAP server.
		/// </summary>
		/// <remarks>This is needed by the IMAP IDLE mechanism to give the server
		/// an indication the connection is still active from time to time.
		/// </remarks>
		private void IssueNoop() {
			string tag = GetTag();
			string response = SendCommandGetResponse(tag + "NOOP");
			while (!response.StartsWith(tag))
				response = GetResponse();
		}

		/// <summary>
		/// Releases all resources used by this ImapClient object.
		/// </summary>
		public void Dispose() {
			stream.Close();
			client.Close();
			stream = null;
			client = null;

			if (idleThread != null) {
				idleThread.Abort();
				idleThread = null;
			}
		}
	}
}
