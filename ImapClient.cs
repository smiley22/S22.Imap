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
using System.Timers;

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
		private readonly object sequenceLock = new object();
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
		private Thread idleThread, idleDispatch;
		private int pauseRefCount = 0;
		private SafeQueue<string> idleEvents = new SafeQueue<string>();
		private System.Timers.Timer noopTimer = new System.Timers.Timer();

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
				capabilities = response.Substring(13).Trim().Split(' ')
					.Select(s => s.ToUpperInvariant()).ToArray();
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
			lock (sequenceLock) {
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
		/// Reads the specified amount of bytes from the server. This
		/// method blocks until the specified amount of bytes has been
		/// read from the network stream.
		/// </summary>
		/// <param name="byteCount">The number of bytes to read</param>
		/// <returns>The read number of bytes as an ASCII-encoded string</returns>
		private string GetData(int byteCount) {
			byte[] buffer = new byte[4096];
			using (var mem = new MemoryStream()) {
				lock (readLock) {
					while (byteCount > 0) {
						int request = byteCount > buffer.Length ?
							buffer.Length : byteCount;
						int read = stream.Read(buffer, 0, request);
						mem.Write(buffer, 0, read);
						byteCount = byteCount - read;
					}
				}
				return Encoding.ASCII.GetString(mem.ToArray());
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
		/// <remarks>This is one of the few methods which can be called in a non-authenticated
		/// state.</remarks>
		public string[] Capabilities() {
			if (capabilities != null)
				return capabilities;
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string command = tag + "CAPABILITY";
				string response = SendCommandGetResponse(command);
				/* Server is required to issue untagged capability response */
				if (response.StartsWith("* CAPABILITY "))
					response = response.Substring(13);
				capabilities = response.Trim().Split(' ')
					.Select(s => s.ToUpperInvariant()).ToArray();
				/* should return OK */
				response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return capabilities;
			}
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
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "RENAME " +
					mailbox.QuoteString() + " " + newName.QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
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
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "DELETE " +
					mailbox.QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
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
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "CREATE " +
					mailbox.QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
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
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "SELECT " +
					mailbox.QuoteString());
				/* evaluate untagged data */
				while (response.StartsWith("*")) {
					// Fixme: evaluate data
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				selectedMailbox = mailbox;
			}
		}

		/// <summary>
		/// Retrieves a list of all available and selectable mailboxes on the server.
		/// </summary>
		/// <returns>A list of all available and selectable mailboxes</returns>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if a list of mailboxes
		/// could not be retrieved. The message property of the exception contains the
		/// error message returned by the server.</exception>
		/// <remarks>The mailbox INBOX is a special name reserved to mean "the
		/// primary mailbox for this user on this server"</remarks>
		public string[] ListMailboxes() {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
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
					bool add = true;
					foreach (string a in attr) {
						/* Only list selectable mailboxes */
						if (a.ToLower() == @"\noselect")
							add = false;
					}
					if (add)
						mailboxes.Add(m.Groups[3].Value);
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return mailboxes.ToArray();
			}
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
		/// <seealso cref="DeleteMessage"/>
		public void Expunge(string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
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
		}

		/// <summary>
		/// Retrieves status information (total number of messages, number of unread
		/// messages, etc.) for the specified mailbox.</summary>
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
		/// <remarks>Not all IMAP servers support the retrieval of quota information. If
		/// it is not possible to retrieve this information, the UsedStorage and FreeStorage
		/// properties of the returned MailboxStatus instance are set to 0.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetStatus"]/*'/>
		public MailboxStatus GetStatus(string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			int messages = 0, unread = 0;
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = defaultMailbox;
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "STATUS " +
					mailbox.QuoteString() + " (MESSAGES UNSEEN)");
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
			}

			/* Collect quota information if server supports it */
			UInt64 usedStorage = 0, freeStorage = 0;

			if (Supports("QUOTA")) {
				MailboxQuota[] Quotas = GetQuota(mailbox);
				foreach (MailboxQuota Q in Quotas) {
					if (Q.ResourceName == "STORAGE") {
						usedStorage = Q.Usage;
						freeStorage = Q.Limit - Q.Usage;
					}
				}
			}

			return new MailboxStatus(messages, unread, usedStorage, freeStorage);
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Search"]/*'/>
		public uint[] Search(SearchCondition criteria, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage-1"]/*'/>
		public MailMessage GetMessage(uint uid, bool seen = true, string mailbox = null) {
			return GetMessage(uid, FetchOptions.Normal, seen, mailbox);
		}
	
		/// <summary>
		/// Retrieves a mail message by its unique identifier message attribute with the
		/// specified fetch option.
		/// </summary>
		/// <param name="uid">The unique identifier of the mail message to retrieve</param>
		/// <param name="options">A value from the FetchOptions enumeration which allows
		/// for fetching selective parts of a mail message.</param>
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.
		/// <para>If you need more fine-grained control over which parts of a mail
		/// message to fetch, consider using one of the overloaded GetMessage methods.
		/// </para>
		/// </remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage-2"]/*'/>
		public MailMessage GetMessage(uint uid, FetchOptions options,
			bool seen = true, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string header = GetMailHeader(uid, seen, mailbox);
				MailMessage message = MessageBuilder.FromHeader(header);
				if (options == FetchOptions.HeadersOnly) {
					ResumeIdling();
					return message;
				}
				/* Retrieve and parse the body structure of the mail message */
				string structure = GetBodystructure(uid, mailbox);
				try {
					Bodypart[] parts = Bodystructure.Parse(structure);
					foreach (Bodypart part in parts) {
						if (options != FetchOptions.Normal &&
							part.Disposition.Type == ContentDispositionType.Attachment)
							continue;
						if (options == FetchOptions.TextOnly && part.Type != ContentType.Text)
							continue;
						/* fetch the content */
						string content = GetBodypart(uid, part.PartNumber, seen, mailbox);

						message.AddBodypart(part, content);
					}
				} catch (FormatException) {
					throw new BadServerResponseException("Server returned erroneous " +
						"body structure:" + structure);
				}
				ResumeIdling();
				return message;
			}
		}

		/// <summary>
		/// Retrieves a mail message by its unique identifier message attribute providing
		/// fine-grained control over which message parts to retrieve.
		/// </summary>
		/// <param name="uid">The unique identifier of the mail message to retrieve</param>
		/// <param name="callback">A delegate which will be invoked for every MIME body
		/// part of the mail message to determine whether it should be fetched from the
		/// server or skipped.</param>
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage-3"]/*'/>
		public MailMessage GetMessage(uint uid, ExaminePartDelegate callback,
			bool seen = true, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string header = GetMailHeader(uid, seen, mailbox);
				MailMessage message = MessageBuilder.FromHeader(header);
				string structure = GetBodystructure(uid, mailbox);
				try {
					Bodypart[] parts = Bodystructure.Parse(structure);
					foreach (Bodypart part in parts) {
						/* Let delegate decide if part should be fetched or not */
						if (callback(part) == true) {
							string content = GetBodypart(uid, part.PartNumber, seen, mailbox);
							message.AddBodypart(part, content);
						}
					}
				} catch (FormatException) {
					throw new BadServerResponseException("Server returned erroneous " +
						"body structure:" + structure);
				}
				ResumeIdling();
				return message;
			}
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages-1"]/*'/>
		public MailMessage[] GetMessages(uint[] uids, bool seen = true, string mailbox = null) {
			return GetMessages(uids, FetchOptions.Normal, seen, mailbox);
		}

		/// <summary>
		/// Retrieves a set of mail messages by their unique identifier message attributes
		/// providing fine-grained control over which message parts to retrieve of each
		/// respective message.
		/// </summary>
		/// <param name="uids">An array of unique identifiers of the mail messages to
		/// retrieve</param>
		/// <param name="callback">A delegate which will be invoked for every MIME body
		/// part of a mail message to determine whether it should be fetched from the
		/// server or skipped.</param>
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages-3"]/*'/>
		public MailMessage[] GetMessages(uint[] uids, ExaminePartDelegate callback,
			bool seen = true, string mailbox = null) {
			List<MailMessage> list = new List<MailMessage>();
			foreach (uint uid in uids)
				list.Add(GetMessage(uid, callback, seen, mailbox));
			return list.ToArray();
		}

		/// <summary>
		/// Retrieves a set of mail messages by their unique identifier message attributes
		/// with the specified fetch option.
		/// </summary>
		/// <param name="uids">An array of unique identifiers of the mail messages to
		/// retrieve</param>
		/// <param name="options">A value from the FetchOptions enumeration which allows
		/// for fetching selective parts of a mail message.</param>
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
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages-2"]/*'/>
		public MailMessage[] GetMessages(uint[] uids, FetchOptions options,
			bool seen = true, string mailbox = null) {
				List<MailMessage> list = new List<MailMessage>();
				foreach (uint uid in uids)
					list.Add(GetMessage(uid, options, seen, mailbox));
				return list.ToArray();
		}

		/// <summary>
		/// Stores the specified mail message on the IMAP server.
		/// </summary>
		/// <param name="message">The mail message to store on the server.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the message
		/// on the server.</param>
		/// <param name="mailbox">The mailbox the message will be stored in. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to store the message in.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message could
		/// not be stored. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>The unique identifier (UID) of the stored message.</returns>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <seealso cref="StoreMessages"/>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="StoreMessage"]/*'/>
		public uint StoreMessage(MailMessage message, bool seen = false, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			string mime822 = message.ToMIME822();
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = defaultMailbox;
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "APPEND " +
					mailbox.QuoteString() + (seen ? @" (\Seen)" : "") +
					" {" + mime822.Length + "}");
				/* Server is required to send a continuation response to signal
				 * we can go ahead with the actual message data */
				if (!response.StartsWith("+"))
					throw new BadServerResponseException(response);
				response = SendCommandGetResponse(mime822);
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return GetHighestUID(mailbox);
			}
		}

		/// <summary>
		/// Stores the specified mail messages on the IMAP server.
		/// </summary>
		/// <param name="messages">An array of mail messages to store on the server.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for each message
		/// on the server.</param>
		/// <param name="mailbox">The mailbox the messages will be stored in. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to store the messages in.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail messages could
		/// not be stored. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>An array containing the unique identifiers (UID) of the stored
		/// messages.</returns>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each
		/// message which uniquely identifies the message within a mailbox. No two
		/// messages in a mailbox share the the same UID.</remarks>
		/// <seealso cref="StoreMessage"/>
		public uint[] StoreMessages(MailMessage[] messages, bool seen = false,
			string mailbox = null) {
			List<uint> list = new List<uint>();
			foreach (MailMessage m in messages)
				list.Add(StoreMessage(m, seen, mailbox));
			return list.ToArray();
		}

		/// <summary>
		/// Retrieves the mail header for a mail message and returns it as a string.
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve the mail
		/// headers for.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched
		/// messages on the server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail header could
		/// not be fetched. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>A string containing the raw mail header of the mail message with the
		/// specified UID.</returns>
		private string GetMailHeader(uint uid, bool seen = true, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				StringBuilder builder = new StringBuilder();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid + " (BODY" +
					(seen ? null : ".PEEK") + "[HEADER])");
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* \d+ FETCH .* {(\d+)}");
					if (m.Success) {
						int size = Convert.ToInt32(m.Groups[1].Value);
						builder.Append(GetData(size));
						if ((response = GetResponse()) != ")")
							throw new BadServerResponseException(response);
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return builder.ToString();
			}
		}

		/// <summary>
		/// Retrieves the body structure for a mail message and returns it as a string.
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve the body structure
		/// for.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the body structure could
		/// not be fetched. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>A string containing the raw body structure of the mail message with the
		/// specified UID.</returns>
		/// <remarks>A body structure is a textual description of the layout of a mail message.
		/// It is described in some detail in RFC 3501 under 7.4.2 FETCH response.</remarks>
		private string GetBodystructure(uint uid, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid +
					" (BODYSTRUCTURE)");
				string structure = String.Empty;
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response,
						@"FETCH \(.*BODYSTRUCTURE \((.*)\).*\)", RegexOptions.IgnoreCase);
					if (m.Success)
						structure = m.Groups[1].Value;
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return structure;
			}
		}

		/// <summary>
		/// Retrieves the MIME body part of a multipart message with the specified
		/// part number.
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve a MIME body part
		/// from.</param>
		/// <param name="partNumber">The part number of the body part to fetch as
		/// is expected by the IMAP server.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched
		/// messages on the server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the body part could
		/// not be fetched. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <returns>A string containing the downloaded body part of the mail message
		/// with the specified UID.</returns>
		private string GetBodypart(uint uid, string partNumber, bool seen = true,
			string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				StringBuilder builder = new StringBuilder();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid +
					" (BODY" + (seen ? null : ".PEEK") + "[" + partNumber + "])");
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* \d+ FETCH .* {(\d+)}");
					if (m.Success) {
						int size = Convert.ToInt32(m.Groups[1].Value);
						builder.Append(GetData(size));
						if ((response = GetResponse()) != ")")
							throw new BadServerResponseException(response);
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return builder.ToString();
			}
		}

		/// <summary>
		/// Retrieves the highest UID in the mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to find the highest UID for. If
		/// this parameter is null, the value of the DefaultMailbox property is
		/// used to determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the UID could
		/// not be determined. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <returns>The highest unique identifier value (UID) in the mailbox</returns>
		/// <remarks>The highest UID usually corresponds to the newest message in a
		/// mailbox.</remarks>
		private uint GetHighestUID(string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "STATUS " +
					selectedMailbox.QuoteString() + " (UIDNEXT)");
				uint nextUID = 0;
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* STATUS.*UIDNEXT (\d+)");
					if (m.Success)
						nextUID = Convert.ToUInt32(m.Groups[1].Value);
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return (nextUID - 1);
			}
		}

		/// <summary>
		/// Copies a mail message with the specified UID to the specified destination
		/// mailbox.
		/// </summary>
		/// <param name="uid">The UID of the mail message that is to be copied.</param>
		/// <param name="destination">The name of the mailbox to copy the message
		/// into.</param>
		/// <param name="mailbox">The mailbox the message will be copied from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message could
		/// not be copied to the specified destination. The message property of the
		/// exception contains the error message returned by the server.</exception>
		/// <seealso cref="MoveMessage"/>
		public void CopyMessage(uint uid, string destination, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID COPY " + uid + " "
					+ destination.QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Moves a mail message with the specified UID to the specified destination
		/// mailbox.
		/// </summary>
		/// <param name="uid">The UID of the mail message that is to be moved.</param>
		/// <param name="destination">The name of the mailbox to move the message
		/// into.</param>
		/// <param name="mailbox">The mailbox the message will be moved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message could
		/// not be moved into the specified destination. The message property of the
		/// exception contains the error message returned by the server.</exception>
		/// <seealso cref="CopyMessage"/>
		/// <seealso cref="DeleteMessage"/>
		public void MoveMessage(uint uid, string destination, string mailbox = null) {
			CopyMessage(uid, destination, mailbox);
			DeleteMessage(uid, mailbox);
		}

		/// <summary>
		/// Deletes the mail message with the specified UID.
		/// </summary>
		/// <param name="uid">The UID of the mail message that is to be deleted.</param>
		/// <param name="mailbox">The mailbox the message will be deleted from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message could
		/// not be deleted. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <seealso cref="MoveMessage"/>
		public void DeleteMessage(uint uid, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" +FLAGS.SILENT (\Deleted \Seen)");
				while (response.StartsWith("*")) {
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Retrieves the IMAP message flag attributes for a mail message.
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve the flag
		/// attributes for.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this
		/// parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message flags
		/// could not be retrieved. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <returns>A list of IMAP flags set for the message with the matching UID.</returns>
		/// <seealso cref="SetMessageFlags"/>
		/// <seealso cref="AddMessageFlags"/>
		/// <seealso cref="RemoveMessageFlags"/>
		public MessageFlag[] GetMessageFlags(uint uid, string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid +
					" (FLAGS)");
				List<MessageFlag> flags = new List<MessageFlag>();
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"FLAGS \(([\w\s\\]*)\)");
					if (!m.Success)
						continue;
					string[] setFlags = m.Groups[1].Value.Split(new char[] { ' ' });
					foreach (string flag in setFlags) {
						if (messageFlagsMapping.ContainsKey(flag))
							flags.Add(messageFlagsMapping[flag]);
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return flags.ToArray();
			}
		}

		/// <summary>
		/// Sets the IMAP message flag attributes for a mail message.
		/// </summary>
		/// <param name="uid">The UID of the mail message to set the flag
		/// attributes for.</param>
		/// <param name="mailbox">The mailbox that contains the mail message. If this
		/// parameter is null, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <param name="flags">One or multiple message flags from the MessageFlag 
		/// enumeration.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message flags
		/// could not be set. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <remarks>This method replaces the current flag attributes of the message
		/// with the specified new flags. If you wish to retain the old attributes, use
		/// the <see cref="AddMessageFlags"/> method instead.</remarks>
		/// <seealso cref="GetMessageFlags"/>
		/// <seealso cref="AddMessageFlags"/>
		/// <seealso cref="RemoveMessageFlags"/>
		public void SetMessageFlags(uint uid, string mailbox, params MessageFlag[] flags) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string flagsString = "";
				foreach (MessageFlag f in flags)
					flagsString = flagsString + @"\" + f + " ";
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" FLAGS.SILENT (" + flagsString.Trim() + ")");
				while (response.StartsWith("*")) {
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Adds the specified set of IMAP message flags to the existing flag attributes
		/// of a mail message.
		/// </summary>
		/// <param name="uid">The UID of the mail message to add the flag
		/// attributes to.</param>
		/// <param name="mailbox">The mailbox that contains the mail message. If this
		/// parameter is null, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <param name="flags">One or multiple message flags from the MessageFlag 
		/// enumeration.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message flags
		/// could not be added. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <remarks>This method adds the specified set of flags to the existing set of
		/// flag attributes of the message. If you wish to replace the old attributes, use
		/// the <see cref="SetMessageFlags"/> method instead.</remarks>
		/// <seealso cref="GetMessageFlags"/>
		/// <seealso cref="SetMessageFlags"/>
		/// <seealso cref="RemoveMessageFlags"/>
		public void AddMessageFlags(uint uid, string mailbox, params MessageFlag[] flags) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string flagsString = "";
				foreach (MessageFlag f in flags)
					flagsString = flagsString + @"\" + f + " ";
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" +FLAGS.SILENT (" + flagsString.Trim() + ")");
				while (response.StartsWith("*")) {
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Removes the specified set of IMAP message flags from the existing flag
		/// attributes of a mail message.
		/// </summary>
		/// <param name="uid">The UID of the mail message to remove the flag
		/// attributes to.</param>
		/// <param name="mailbox">The mailbox that contains the mail message. If this
		/// parameter is null, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <param name="flags">One or multiple message flags from the MessageFlag 
		/// enumeration.</param>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the mail message flags
		/// could not be removed. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <remarks>This method removes the specified set of flags from the existing set of
		/// flag attributes of the message. If you wish to replace the old attributes, use
		/// the <see cref="SetMessageFlags"/> method instead.</remarks>
		/// <seealso cref="GetMessageFlags"/>
		/// <seealso cref="SetMessageFlags"/>
		/// <seealso cref="AddMessageFlags"/>
		public void RemoveMessageFlags(uint uid, string mailbox, params MessageFlag[] flags) {
			if (!Authed)
				throw new NotAuthenticatedException();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string flagsString = "";
				foreach (MessageFlag f in flags)
					flagsString = flagsString + @"\" + f + " ";
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" -FLAGS.SILENT (" + flagsString.Trim() + ")");
				while (response.StartsWith("*")) {
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// A mapping to map IMAP message flag attribute values to their 
		/// corresponding MessageFlag enum counterparts.
		/// </summary>
		static private Dictionary<string, MessageFlag> messageFlagsMapping =
			new Dictionary<string, MessageFlag>(StringComparer.OrdinalIgnoreCase) {
				{ @"\Seen", MessageFlag.Seen },
				{ @"\Answered", MessageFlag.Answered },
				{ @"\Flagged", MessageFlag.Flagged },
				{ @"\Deleted", MessageFlag.Deleted },
				{ @"\Draft", MessageFlag.Draft },
				{ @"\Recent",	MessageFlag.Recent }
			};

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
			if (!Supports("IDLE"))
				throw new InvalidOperationException("The server does not support the " +
					"IMAP4 IDLE command");
			lock (sequenceLock) {
				/* Make sure a mailbox is selected */
				SelectMailbox(null);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "IDLE");
				/* Server must respond with a '+' continuation response */
				if (!response.StartsWith("+"))
					throw new BadServerResponseException(response);
			}
			/* setup and start the idle thread */
			if (idleThread != null)
				throw new ApplicationException("idleThread is not null");
			idling = true;
			idleThread = new Thread(IdleLoop);
			idleThread.IsBackground = true;
			idleThread.Start();
			/* setup a timer to issue NOOPs every once in a while */
			noopTimer.Interval = 1000 * 60 * 10;
			noopTimer.Elapsed += IssueNoop;
			noopTimer.Start();
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
			if (!Authed)
				throw new NotAuthenticatedException();
			if (!idling)
				return;
			SendCommand("DONE");
			/* Wait until idle thread has shutdown */
			idleThread.Join();
			idleThread = null;
			idling = false;
			noopTimer.Stop();
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
			pauseRefCount = pauseRefCount + 1;
			if (pauseRefCount != 1)
				return;
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
			pauseRefCount = pauseRefCount - 1;
			if (pauseRefCount != 0)
				return;
			/* Make sure a mailbox is selected */
			lock (sequenceLock) {
				SelectMailbox(null);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "IDLE");
				/* Server must respond with a '+' continuation response */
				if (!response.StartsWith("+"))
					throw new BadServerResponseException(response);
			}
			/* setup and start the idle thread */
			if (idleThread != null)
				throw new ApplicationException("idleThread is not null");
			idleThread = new Thread(IdleLoop);
			idleThread.IsBackground = true;
			idleThread.Start();
		}

		/// <summary>
		/// The main idle loop. Waits for incoming IMAP IDLE notifications and dispatches
		/// them as events. This runs in its own thread whenever IMAP IDLE
		/// notifications are to be received.
		/// </summary>
		private void IdleLoop() {
			if (idleDispatch == null) {
				idleDispatch = new Thread(EventDispatcher);
				idleDispatch.IsBackground = true;
				idleDispatch.Start();
			}

			while (true) {
				try {
					string response = GetResponse();
					/* A request was made to stop idling so quit the thread */
					if (response.Contains("OK IDLE"))
						return;
					/* Let the dispatcher thread take care of the IDLE notification so we
					 * can go back to receiving responses */
					idleEvents.Enqueue(response);
				} catch (IOException) {
					/* Closing _Stream or the underlying _Connection instance will
					 * cause a WSACancelBlockingCall exception on a blocking socket.
					 * This is not an error so just let it pass.
					 */
				}
			}
		}

		/// <summary>
		/// Blocks on a queue and wakes up whenever a new notification is put into the
		/// queue. The notification is then examined and dispatched as an event.
		/// </summary>
		private void EventDispatcher() {
			while (true) {
				string response = idleEvents.Dequeue();
				Match m = Regex.Match(response, @"\*\s+(\d+)\s+(\w+)");
				if (!m.Success)
					continue;
				uint numberOfMessages = Convert.ToUInt32(m.Groups[1].Value);
				switch (m.Groups[2].Value.ToUpper()) {
					case "EXISTS":
						newMessageEvent.Raise(this,
							new IdleMessageEventArgs(numberOfMessages, GetHighestUID(), this));
						break;
					case "EXPUNGE":
						messageDeleteEvent.Raise(
							this, new IdleMessageEventArgs(numberOfMessages, GetHighestUID(), this));
						break;
				}
			}
		}

		/// <summary>
		/// Issues a NOOP command to the IMAP server. Called in the context of a
		/// System.Timer event when IDLE notifications are being received.
		/// </summary>
		/// <remarks>This is needed by the IMAP IDLE mechanism to give the server
		/// an indication the connection is still active.
		/// </remarks>
		private void IssueNoop(object sender, ElapsedEventArgs e) {
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "NOOP");
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Retrieves IMAP QUOTA information for a mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to retrieve QUOTA information for.
		/// If this parameter is null, the value of the DefaultMailbox property is
		/// used to determine the mailbox to operate on.</param>
		/// <returns>A list of MailboxQuota objects describing usage and limits
		/// of the quota roots for the mailbox.</returns>
		/// <exception cref="NotAuthenticatedException">Thrown if the method was called
		/// in a non-authenticated state, i.e. before logging into the server with
		/// valid credentials.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the IMAP4 QUOTA
		/// extension is not supported by the server.</exception>
		/// <exception cref="BadServerResponseException">Thrown if the quota operation
		/// could not be completed. The message property of the exception contains the error
		/// message returned by the server.</exception>
		private MailboxQuota[] GetQuota(string mailbox = null) {
			if (!Authed)
				throw new NotAuthenticatedException();
			if (!Supports("QUOTA"))
				throw new InvalidOperationException(
					"This server does not support the IMAP4 QUOTA extension");
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = DefaultMailbox;
				List<MailboxQuota> Quotas = new List<MailboxQuota>();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "GETQUOTAROOT " +
					mailbox.QuoteString());
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response,
						"\\* QUOTA \"(\\w*)\" \\((\\w+)\\s+(\\d+)\\s+(\\d+)\\)");
					if (m.Success) {
						try {
							MailboxQuota Quota = new MailboxQuota(m.Groups[2].Value,
								UInt32.Parse(m.Groups[3].Value),
								UInt32.Parse(m.Groups[4].Value));
							Quotas.Add(Quota);
						} catch {
							throw new BadServerResponseException(response);
						}
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return Quotas.ToArray();
			}
		}

		/// <summary>
		/// Releases all resources used by this ImapClient object.
		/// </summary>
		public void Dispose() {
			if (idleThread != null) {
				idleThread.Abort();
				idleThread = null;
			}
			if (idleDispatch != null) {
				idleDispatch.Abort();
				idleDispatch = null;
			}
			stream.Close();
			client.Close();
			stream = null;
			client = null;
		}
	}

	/// <summary>
	/// A delegate which is invoked during a call to GetMessage or GetMessages for every
	/// MIME part in a multipart mail message. The delegate can examine the MIME body
	/// part and decide to either include it in the returned mail message or dismiss
	/// it.
	/// </summary>
	/// <param name="part">A MIME body part of a mail message which consists of multiple
	/// parts.</param>
	/// <returns>Return true to include the body part in the returned MailMessage object,
	/// or false to skip it.</returns>
	public delegate bool ExaminePartDelegate(Bodypart part);
}
