using S22.Imap.Auth;
using S22.Imap.Auth.Sasl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace S22.Imap {
	/// <summary>
	/// Enables applications to communicate with a mail server using the Internet Message Access
	/// Protocol (IMAP).
	/// </summary>
	public class ImapClient : IImapClient
	{
		Stream stream;
		TcpClient client;
		bool disposed;
		readonly object readLock = new object();
		readonly object writeLock = new object();
		readonly object sequenceLock = new object();
		string[] capabilities;
		int tag = 0;
		string selectedMailbox;
		string defaultMailbox = "INBOX";
		event EventHandler<IdleMessageEventArgs> newMessageEvent;
		event EventHandler<IdleMessageEventArgs> messageDeleteEvent;
		bool hasEvents {
			get {
				return newMessageEvent != null || messageDeleteEvent != null;
			}
		}
		bool idling;
		Thread idleThread, idleDispatch;
		int pauseRefCount = 0;
		SafeQueue<string> idleEvents = new SafeQueue<string>();
		System.Timers.Timer noopTimer = new System.Timers.Timer();
		static readonly TraceSource ts = new TraceSource("S22.Imap");

		/// <summary>
		/// The default mailbox to operate on.
		/// </summary>
		/// <exception cref="ArgumentNullException">The property is being set and the value is
		/// null.</exception>
		/// <exception cref="ArgumentException">The property is being set and the value is the empty
		/// string.</exception>
		/// <remarks>The default value for this property is "INBOX" which is a special name reserved
		/// to mean "the primary mailbox for this user on this server".</remarks>
		public string DefaultMailbox {
			get {
				return defaultMailbox;
			}
			set {
				value.ThrowIfNullOrEmpty();
				defaultMailbox = value;
			}
		}

		/// <summary>
		/// Determines whether the client is authenticated with the server.
		/// </summary>
		public bool Authed {
			get;
			private set;
		}

		/// <summary>
		/// The event that is raised when a new mail message has been received by the server.
		/// </summary>
		/// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
		/// method can be used, specifying "IDLE" for the capability parameter.
		/// 
		/// Please note that the event handler will be executed on a threadpool thread.
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
		/// The event that is raised when a message has been deleted on the server.
		/// </summary>
		/// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
		/// method can be used, specifying "IDLE" for the capability parameter.
		/// 
		/// Please note that the event handler will be executed on a threadpool thread.
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
		/// The event that is raised when an I/O exception occurs in the idle-thread.
		/// </summary>
		/// <remarks>
		/// An I/O exception can occur if the underlying network connection has been reset or the
		/// server unexpectedly closed the connection.
		/// </remarks>
		public event EventHandler<IdleErrorEventArgs> IdleError;

		/// <summary>
		/// This constructor is solely used for unit testing.
		/// </summary>
		/// <param name="stream">A stream to initialize the ImapClient instance with.</param>
		internal ImapClient(Stream stream) {
			this.stream = stream;
			Authed = true;
		}

		/// <summary>
		/// Initializes a new instance of the ImapClient class and connects to the specified port
		/// on the specified host, optionally using the Secure Socket Layer (SSL) security protocol.
		/// </summary>
		/// <param name="hostname">The DNS name of the server to which you intend to connect.</param>
		/// <param name="port">The port number of the server to which you intend to connect.</param>
		/// <param name="ssl">Set to true to use the Secure Socket Layer (SSL) security protocol.</param>
		/// <param name="validate">Delegate used for verifying the remote Secure Sockets
		/// Layer (SSL) certificate which is used for authentication. Can be null if not needed.</param>
		/// <exception cref="ArgumentOutOfRangeException">The port parameter is not between MinPort
		/// and MaxPort.</exception>
		/// <exception cref="ArgumentNullException">The hostname parameter is null.</exception>
		/// <exception cref="BadServerResponseException">An unexpected response has been received from
		/// the server upon connecting.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="SocketException">An error occurred while accessing the socket used for
		/// establishing the connection to the IMAP server. Use the ErrorCode property to obtain the
		/// specific error code.</exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">An authentication
		/// error occured while trying to establish a secure connection.</exception>
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
		/// (SSL) certificate which is used for authentication. Can be null if not needed.</param>
		/// <exception cref="ArgumentOutOfRangeException">The port parameter is not between MinPort
		/// and MaxPort.</exception>
		/// <exception cref="ArgumentNullException">The hostname parameter is null.</exception>
		/// <exception cref="BadServerResponseException">An unexpected response has been received from
		/// the server upon connecting.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="InvalidCredentialsException">The provided credentials were rejected by the
		/// server.</exception>
		/// <exception cref="SocketException">An error occurred while accessing the socket used for
		/// establishing the connection to the IMAP server. Use the ErrorCode property to obtain the
		/// specific error code.</exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">An authentication
		/// error occured while trying to establish a secure connection.</exception>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="ctor-2"]/*'/>
		public ImapClient(string hostname, int port, string username, string password, AuthMethod method =
			AuthMethod.Auto, bool ssl = false, RemoteCertificateValidationCallback validate = null) {
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
		/// <param name="validate">Delegate used for verifying the remote Secure Sockets Layer (SSL)
		/// certificate which is used for authentication. Can be null if not needed.</param>
		/// <exception cref="ArgumentOutOfRangeException">The port parameter is not between MinPort
		/// and MaxPort.</exception>
		/// <exception cref="ArgumentNullException">The hostname parameter is null.</exception>
		/// <exception cref="BadServerResponseException">An unexpected response has been received
		/// from the server upon connecting.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="SocketException">An error occurred while accessing the socket used for
		/// establishing the connection to the IMAP server. Use the ErrorCode property to obtain the
		/// specific error code.</exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">An authentication
		/// error occured while trying to establish a secure connection.</exception>
		void Connect(string hostname, int port, bool ssl, RemoteCertificateValidationCallback validate) {
			client = new TcpClient(hostname, port);
			stream = client.GetStream();
			if (ssl) {
				SslStream sslStream = new SslStream(stream, false, validate ??
					((sender, cert, chain, err) => true));
				sslStream.AuthenticateAsClient(hostname);
				stream = sslStream;
			}
			// The server issues an untagged OK greeting upon connect.
			string greeting = GetResponse();
			if (!IsResponseOK(greeting))
				throw new BadServerResponseException(greeting);
		}

		/// <summary>
		/// Determines whether the specified response is a valid IMAP OK response.
		/// </summary>
		/// <param name="response">A response string received from the server.</param>
		/// <param name="tag">A tag if the response is associated with a command.</param>
		/// <returns>true if the response is a valid IMAP OK response; Otherwise false.</returns>
		bool IsResponseOK(string response, string tag = null) {
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
		/// <exception cref="ArgumentNullException">The username parameter or the password parameter
		/// is null.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="InvalidCredentialsException">The server rejected the supplied
		/// credentials.</exception>
		/// <exception cref="NotSupportedException">The specified authentication method is not
		/// supported by the server.</exception>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Login"]/*'/>
		public void Login(string username, string password, AuthMethod method) {
			username.ThrowIfNull("username");
			password.ThrowIfNull("password");
			string tag = GetTag(), response;
			switch (method) {
				case AuthMethod.Login:
					response = Login(tag, username, password);
					break;
				case AuthMethod.Auto:
					response = AuthAuto(tag, username, password);
					break;
				case AuthMethod.NtlmOverSspi:
					response = SspiAuthenticate(tag, username, password, true);
					break;
				case AuthMethod.Gssapi:
					response = SspiAuthenticate(tag, username, password, false);
					break;
				default:
					response = Authenticate(tag, username, password, method.ToString());
					break;
			}
			// The server may include an untagged CAPABILITY line in the response.
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
		/// Performs authentication using the most secure authentication mechanism supported by the
		/// server.
		/// </summary>
		/// <param name="tag">The tag identifier to use for performing the authentication
		/// commands.</param>
		/// <param name="username">The username with which to login in to the IMAP server.</param>
		/// <param name="password">The password with which to log in to the IMAP server.</param>
		/// <returns>The response sent by the server.</returns>
		/// <remarks>The order of preference of authentication types employed by this method is
		/// Ntlm, Scram-Sha-1, Digest-Md5, followed by Cram-Md5 and finally plaintext Login as
		/// a last resort.</remarks>
		string AuthAuto(string tag, string username, string password) {
			string[] methods = new string[] { "Srp", "Ntlm", "ScramSha1", "DigestMd5",
				"CramMd5" };
			foreach (string m in methods) {
				try {
					string response = Authenticate(tag, username, password, m);
					if (IsResponseOK(response, tag) || response.StartsWith("* CAPABILITY"))
						return response;
				} catch {
					// Go on with next method.
				}
			}
			// If all of the above failed, use login as a last resort.
			return Login(tag, username, password);
		}

		/// <summary>
		/// Performs an actual IMAP "LOGIN" command using the specified username and plain-text
		/// password.
		/// </summary>
		/// <param name="tag">The tag identifier to use for performing the authentication
		/// commands.</param>
		/// <param name="username">The username with which to login in to the IMAP server.</param>
		/// <param name="password">The password with which to log in to the IMAP server.</param>
		/// <returns>The response sent by the server.</returns>
		string Login(string tag, string username, string password) {
			return SendCommandGetResponse(tag + "LOGIN " + username.QuoteString() +
				" " + password.QuoteString());
		}

		/// <summary>
		/// Performs NTLM and Kerberos authentication via the Security Support Provider Interface (SSPI).
		/// </summary>
		/// <param name="tag">The tag identifier to use for performing the authentication
		/// commands.</param>
		/// <param name="username">The username with which to login in to the IMAP server.</param>
		/// <param name="password">The password with which to log in to the IMAP server.</param>
		/// <param name="useNtlm">True to authenticate using NTLM, otherwise GSSAPI (Kerberos) is
		/// used.</param>
		/// <returns>The response sent by the server.</returns>
		/// <exception cref="NotSupportedException">The specified authentication method is not
		/// supported by the server.</exception>
		string SspiAuthenticate(string tag, string username, string password,
			bool useNtlm) {
			string response = SendCommandGetResponse(tag + "AUTHENTICATE " + (useNtlm ?
				"NTLM" : "GSSAPI"));
			// If we get a BAD or NO response, the mechanism is not supported.
			if (response.StartsWith(tag + "BAD") || response.StartsWith(tag + "NO")) {
				throw new NotSupportedException("The requested authentication " +
					"mechanism is not supported by the server.");
			}
			using (FilterStream fs = new FilterStream(stream, true)) {
				using (NegotiateStream ns = new NegotiateStream(fs, true)) {
					try {
						ns.AuthenticateAsClient(
							new NetworkCredential(username, password),
							null,
							String.Empty,
							useNtlm ? ProtectionLevel.None : ProtectionLevel.EncryptAndSign,
							System.Security.Principal.TokenImpersonationLevel.Delegation);
					} catch {
						return String.Empty;
					}
				}
			}
			response = GetResponse();
			// Swallow any continuation data we unexpectedly receive from the server.
			while (response.StartsWith("+ "))
				response = SendCommandGetResponse(String.Empty);
			return response;
		}

		/// <summary>
		/// Performs authentication using a SASL authentication mechanism via IMAP's authenticate
		/// command.
		/// </summary>
		/// <param name="tag">The tag identifier to use for performing the authentication
		/// commands.</param>
		/// <param name="username">The username with which to login in to the IMAP server.</param>
		/// <param name="password">The password with which to log in to the IMAP server.</param>
		/// <param name="mechanism">The name of the SASL authentication mechanism to use.</param>
		/// <returns>The response sent by the server.</returns>
		/// <exception cref="SaslException">The authentication mechanism with the specified name could
		/// not be found.</exception>
		/// <exception cref="NotSupportedException">The specified authentication mechanism is not
		/// supported by the server.</exception>
		/// <exception cref="BadServerResponseException">An unexpected response has been received from
		/// the server.</exception>
		string Authenticate(string tag, string username, string password,
			string mechanism) {
			SaslMechanism m = SaslFactory.Create(mechanism);
			if (!Supports("Auth=" + m.Name))
				throw new NotSupportedException("The requested authentication " +
					"mechanism is not supported by the server.");
			m.Properties.Add("Username", username);
			m.Properties.Add("Password", password);
			// OAuth and OAuth2 use access tokens.
			m.Properties.Add("AccessToken", password);
			string response = SendCommandGetResponse(tag + "AUTHENTICATE " + m.Name);
			// If we get a BAD or NO response, the mechanism is not supported.
			if (response.StartsWith(tag + "BAD") || response.StartsWith(tag + "NO")) {
				throw new NotSupportedException("The requested authentication " +
					"mechanism is not supported by the server.");
			}
			while (!m.IsCompleted) {
				// Annoyingly, Gmail OAUTH2 issues an untagged capability response during the SASL
				// authentication process. As per spec this is illegal, but we should still deal with it.
				while (response.StartsWith("*"))
					response = GetResponse();
				// Stop if the server response starts with our tag.
				if (response.StartsWith(tag))
					break;
				// Strip off continuation request '+'-character and possible whitespace.
				string challenge = Regex.Replace(response, @"^\+\s?", String.Empty);
				// Compute and send off the challenge-response.
				response = m.GetResponse(challenge);
				response = SendCommandGetResponse(response);
			}
			return response;
		}

		/// <summary>
		/// Logs an authenticated client out of the server. After the logout sequence has been completed,
		/// the server closes the connection with the client.
		/// </summary>
		/// <exception cref="BadServerResponseException">An unexpected response has been received from
		/// the server during the logout sequence.</exception>
		/// <remarks>Calling this method in non-authenticated state has no effect.</remarks>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		public void Logout() {
			AssertValid(false);
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
		/// Generates a unique identifier to prefix a command with, as is required by the IMAP protocol.
		/// </summary>
		/// <returns>A unique identifier string.</returns>
		string GetTag() {
			Interlocked.Increment(ref tag);
			return string.Format("xm{0:000} ", tag);
		}

		/// <summary>
		/// Sends a command string to the server. This method blocks until the command has been
		/// transmitted.
		/// </summary>
		/// <param name="command">The command to send to the server. The string is suffixed by CRLF
		/// prior to sending.</param>
		void SendCommand(string command) {
			ts.TraceInformation("C -> " + command);
			// We can safely use UTF-8 here since it's backwards compatible with ASCII and comes in handy
			// when sending strings in literal form (see RFC 3501, 4.3).
			byte[] bytes = Encoding.UTF8.GetBytes(command + "\r\n");
			lock (writeLock) {
				stream.Write(bytes, 0, bytes.Length);
			}
		}

		/// <summary>
		/// Sends a command string to the server and subsequently waits for a response, which is then
		/// returned to the caller. This method blocks until the server response has been received.
		/// </summary>
		/// <param name="command">The command to send to the server. This is suffixed by CRLF prior
		/// to sending.</param>
		/// <param name="resolveLiterals">Set to true to resolve possible literals returned by the
		/// server (Refer to RFC 3501 Section 4.3 for details).</param>
		/// <returns>The response received by the server.</returns>
		string SendCommandGetResponse(string command, bool resolveLiterals = true) {
			lock (readLock) {
				lock (writeLock) {
					SendCommand(command);
				}
				return GetResponse(resolveLiterals);
			}
		}

		/// <summary>
		/// Waits for a response from the server. This method blocks until a response has been received.
		/// </summary>
		/// <param name="resolveLiterals">Set to true to resolve possible literals returned by the
		/// server (Refer to RFC 3501 Section 4.3 for details).</param>
		/// <returns>A response string from the server</returns>
		/// <exception cref="IOException">The underlying socket is closed or there was a failure
		/// reading from the network.</exception>
		string GetResponse(bool resolveLiterals = true) {
			const int Newline = 10, CarriageReturn = 13;
			using (var mem = new MemoryStream()) {
				lock (readLock) {
					while (true) {
						int i = stream.ReadByte();
						if (i == -1)
							throw new IOException("The stream could not be read.");
						byte b = (byte)i;
						if (b == CarriageReturn)
							continue;
						if (b == Newline) {
							string s = Encoding.ASCII.GetString(mem.ToArray());
							if (resolveLiterals) {
								s = Regex.Replace(s, @"{(\d+)}$", m => {
									return "\"" + GetData(Convert.ToInt32(m.Groups[1].Value)) +
										"\"" + GetResponse(false);
								});
							}
							ts.TraceInformation("S -> " + s);
							return s;
						} else
							mem.WriteByte(b);
					}
				}
			}
		}

		/// <summary>
		/// Reads the specified amount of bytes from the server. This method blocks until the specified
		/// amount of bytes has been read from the network stream.
		/// </summary>
		/// <param name="byteCount">The number of bytes to read.</param>
		/// <returns>The read bytes as an ASCII-encoded string.</returns>
		/// <exception cref="IOException">The underlying socket is closed or there was a failure
		/// reading from the network.</exception>
		string GetData(int byteCount) {
			byte[] buffer = new byte[4096];
			using (var mem = new MemoryStream()) {
				lock (readLock) {
					while (byteCount > 0) {
						int request = byteCount > buffer.Length ? buffer.Length : byteCount;
						int read = stream.Read(buffer, 0, request);
						mem.Write(buffer, 0, read);
						byteCount = byteCount - read;
					}
				}
				string s = Encoding.ASCII.GetString(mem.ToArray());
				ts.TraceInformation("S -> " + s);
				return s;
			}
		}

		/// <summary>
		/// Returns an enumerable collection of capabilities the IMAP server supports. All strings in
		/// the returned collection are guaranteed to be upper-case.
		/// </summary>
		/// <exception cref="BadServerResponseException">An unexpected response has been received from
		/// the server; The message property of the exception contains the error message returned by
		/// the server.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <returns>An enumerable collection of capabilities supported by the server.</returns>
		/// <remarks>This method may be called in non-authenticated state.</remarks>
		public IEnumerable<string> Capabilities() {
			AssertValid(false);
			if (capabilities != null)
				return capabilities;
			lock (sequenceLock) {
				if(Authed)
					PauseIdling();
				string tag = GetTag();
				string command = tag + "CAPABILITY";
				string response = SendCommandGetResponse(command);
				while (response.StartsWith("*")) {
					// The server is required to issue an untagged capability response.
					if (response.StartsWith("* CAPABILITY ")) {
						capabilities = response.Substring(13).Trim().Split(' ')
							.Select(s => s.ToUpperInvariant()).ToArray();
					}
					response = GetResponse();
				}
				if(Authed)
					ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return capabilities;
			}
		}

		/// <summary>
		/// Determines whether the specified capability is supported by the server.
		/// </summary>
		/// <param name="capability">The IMAP capability to probe for (for example "IDLE").</param>
		/// <exception cref="ArgumentNullException">The capability parameter is null.</exception>
		/// <exception cref="BadServerResponseException">An unexpected response has been received from
		/// the server; The message property of the exception contains the error message returned by
		/// the server.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <returns>true if the specified capability is supported by the server; Otherwise
		/// false.</returns>
		/// <remarks>This method may be called in non-authenticated state.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="ctor-2"]/*'/>
		public bool Supports(string capability) {
			AssertValid(false);
			capability.ThrowIfNull("capability");
			return (capabilities ?? Capabilities()).Contains(capability,
				StringComparer.InvariantCultureIgnoreCase);
		}

		/// <summary>
		/// Changes the name of the specified mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to rename.</param>
		/// <param name="newName">The new name the mailbox will be renamed to.</param>
		/// <exception cref="ArgumentNullException">The mailbox parameter or the
		/// newName parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mailbox could not be renamed; The message
		/// property of the exception contains the error message returned by the server.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		public void RenameMailbox(string mailbox, string newName) {
			AssertValid();
			mailbox.ThrowIfNull("mailbox");
			newName.ThrowIfNull("newName");
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "RENAME " +
					Util.UTF7Encode(mailbox).QuoteString() + " " +
					Util.UTF7Encode(newName).QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Permanently removes the specified mailbox.
		/// </summary>
		/// <param name="mailbox">The name of the mailbox to remove.</param>
		/// <exception cref="ArgumentNullException">The mailbox parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The specified mailbox could not be removed.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		public void DeleteMailbox(string mailbox) {
			AssertValid();
			mailbox.ThrowIfNull("mailbox");
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "DELETE " +
					Util.UTF7Encode(mailbox).QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Creates a new mailbox with the specified name.
		/// </summary>
		/// <param name="mailbox">The name of the mailbox to create.</param>
		/// <exception cref="ArgumentNullException">The mailbox parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mailbox with the specified name could
		/// not be created. The message property of the exception contains the error message returned
		/// by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		public void CreateMailbox(string mailbox) {
			AssertValid();
			mailbox.ThrowIfNull("mailbox");
			lock (sequenceLock) {
				PauseIdling();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "CREATE " +
					Util.UTF7Encode(mailbox).QuoteString());
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Selects the specified mailbox so that the messages of the mailbox can be accessed.
		/// </summary>
		/// <param name="mailbox">The mailbox to select. If this parameter is null, the
		/// default mailbox is selected.</param>
		/// <exception cref="BadServerResponseException">The specified mailbox could not be selected.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>IMAP Idle must be paused or stopped before calling this method.</remarks>
		void SelectMailbox(string mailbox) {
			AssertValid();
			if (mailbox == null)
				mailbox = defaultMailbox;
			// The requested mailbox is already selected.
			if (selectedMailbox == mailbox)
				return;
			lock (sequenceLock) {
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "SELECT " +
					Util.UTF7Encode(mailbox).QuoteString());
				while (response.StartsWith("*"))
					response = GetResponse();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				selectedMailbox = mailbox;
			}
		}

		/// <summary>
		/// Retrieves a list of all available and selectable mailboxes on the server.
		/// </summary>
		/// <returns>An enumerable collection of the names of all available and selectable
		/// mailboxes.</returns>
		/// <exception cref="BadServerResponseException">The list of mailboxes could not be retrieved.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>The mailbox name "INBOX" is a special name reserved to mean "the primary mailbox
		/// for this user on this server".</remarks>
		public IEnumerable<string> ListMailboxes() {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				List<string> mailboxes = new List<string>();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "LIST \"\" \"*\"");
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, "\\* LIST \\((.*)\\)\\s+\"([^\"]+)\"\\s+(.+)");
					if (m.Success) {
						string[] attr = m.Groups[1].Value.Split(' ');
						bool add = true;
						foreach (string a in attr) {
							// Only list mailboxes that can actually be selected.
							if (a.ToLower() == @"\noselect")
								add = false;
						}
						// Names _should_ be enclosed in double-quotes but not all servers follow through with
						// this, so we don't enforce it in the above regex.
						string name = Regex.Replace(m.Groups[3].Value, "^\"(.+)\"$", "$1");
						try {
							name = Util.UTF7Decode(name);
						} catch {
							// Include the unaltered string in the result if UTF-7 decoding failed for any reason.
						}
						if (add)
							mailboxes.Add(name);
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return mailboxes;
			}
		}

		/// <summary>
		/// Permanently removes all messages that have the \Deleted flag set from the specified mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to remove all messages from that have the \Deleted flag
		/// set. If this parameter is omitted, the value of the DefaultMailbox property is used to
		/// determine the mailbox to operate on.</param>
		/// <exception cref="BadServerResponseException">The expunge operation could not be completed.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="DeleteMessage"/>
		public void Expunge(string mailbox = null) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "EXPUNGE");
				// The server is required to send an untagged response for each message which is
				// deleted before sending OK.
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Retrieves status information (total number of messages, various attributes as well as quota
		/// information) for the specified mailbox.</summary>
		/// <param name="mailbox">The mailbox to retrieve status information for. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>A MailboxInfo object containing status information for the mailbox.</returns>
		/// <exception cref="BadServerResponseException">The operation could not be completed because
		/// the server returned an error. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>Not all IMAP servers support the retrieval of quota information. If it is not
		/// possible to retrieve this information, the UsedStorage and FreeStorage properties of the
		/// returned MailboxStatus instance are set to 0.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMailboxInfo"]/*'/>
		public MailboxInfo GetMailboxInfo(string mailbox = null) {
			AssertValid();
			// This is not a cheap method to call, it involves a couple of round-trips to the server.
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = defaultMailbox;
				MailboxStatus status = GetMailboxStatus(mailbox);
				// Collect quota information if the server supports it.
				UInt64 used = 0, free = 0;
				if (Supports("QUOTA")) {
					IEnumerable<MailboxQuota> quotas = GetQuota(mailbox);
					foreach (MailboxQuota q in quotas) {
						if (q.ResourceName == "STORAGE") {
							used = q.Usage;
							free = q.Limit - q.Usage;
						}
					}
				}
				// Try to collect special-use flags.
				IEnumerable<MailboxFlag> flags = GetMailboxFlags(mailbox);
				ResumeIdling();
				return new MailboxInfo(mailbox, flags, status.Messages, status.Unread, status.NextUID,
					used, free);
			}
		}

		/// <summary>
		/// Retrieves the set of special-use flags associated with the specified mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to receive the special-use flags for. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <exception cref="BadServerResponseException">The operation could not be completed because
		/// the server returned an error. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <returns>An enumerable collection of special-use flags set on the specified
		/// mailbox.</returns>
		/// <remarks>This feature is an optional extension to the IMAP protocol and as such some servers
		/// may not report any flags at all.</remarks>
		IEnumerable<MailboxFlag> GetMailboxFlags(string mailbox = null) {
			Dictionary<string, MailboxFlag> dictFlags =
				new Dictionary<string, MailboxFlag>(StringComparer.OrdinalIgnoreCase) {
				{ @"\All", MailboxFlag.AllMail },	{ @"\AllMail", MailboxFlag.AllMail },
				{ @"\Archive", MailboxFlag.Archive }, { @"\Drafts", MailboxFlag.Drafts },
				{ @"\Junk", MailboxFlag.Spam },	{ @"\Spam", MailboxFlag.Spam },
				{ @"\Trash", MailboxFlag.Trash },	{ @"\Rubbish", MailboxFlag.Trash },
				{ @"\Sent", MailboxFlag.Sent },	{ @"\SentItems", MailboxFlag.Sent }
			};
			List<MailboxFlag> list = new List<MailboxFlag>();
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = defaultMailbox;
				string tag = GetTag();
				// Use XLIST if server supports it, otherwise at least try LIST and hope server implements
				// the "LIST Extension for Special-Use Mailboxes" (Refer to RFC6154).
				string command = Supports("XLIST") ? "XLIST" : "LIST";
				string response = SendCommandGetResponse(tag + command + " \"\" " +
					Util.UTF7Encode(mailbox).QuoteString());
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, "\\* X?LIST \\((.*)\\)\\s+\"([^\"]+)\"\\s+(.+)");
					if (m.Success) {
						string[] flags = m.Groups[1].Value.Split(' ');
						foreach (string f in flags) {
							if (dictFlags.ContainsKey(f))
								list.Add(dictFlags[f]);
						}
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
			return list;
		}

		/// <summary>
		/// Retrieves status information (total number of messages, number of unread messages, etc.) for
		/// the specified mailbox.</summary>
		/// <param name="mailbox">The mailbox to retrieve status information for. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>A MailboxStatus object containing status information for the mailbox.</returns>
		/// <exception cref="BadServerResponseException">The operation could not be completed because
		/// the server returned an error. The message property of the exception contains the error
		/// message returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		MailboxStatus GetMailboxStatus(string mailbox = null) {
			AssertValid();
			int messages = 0, unread = 0;
			uint uid = 0;
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = defaultMailbox;
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "STATUS " +
					Util.UTF7Encode(mailbox).QuoteString() + " (MESSAGES UNSEEN UIDNEXT)");
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* STATUS.*MESSAGES (\d+)");
					if (m.Success)
						messages = Convert.ToInt32(m.Groups[1].Value);
					m = Regex.Match(response, @"\* STATUS.*UNSEEN (\d+)");
					if (m.Success)
						unread = Convert.ToInt32(m.Groups[1].Value);
					m = Regex.Match(response, @"\* STATUS.*UIDNEXT (\d+)");
					if (m.Success)
						uid = Convert.ToUInt32(m.Groups[1].Value);
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
			return new MailboxStatus(messages, unread, uid);
		}

		/// <summary>
		/// Searches the specified mailbox for messages that match the given search criteria.
		/// </summary>
		/// <param name="criteria">A search criteria expression; Only messages that match this
		/// expression will be included in the result set returned by this method.</param>
		/// <param name="mailbox">The mailbox that will be searched. If this parameter is omitted, the
		/// value of the DefaultMailbox property is used to determine the mailbox to operate on.</param>
		/// <exception cref="ArgumentNullException">The criteria parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The search could not be completed. The message
		/// property of the exception contains the error message returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <exception cref="NotSupportedException">The search values contain characters beyond the
		/// ASCII range and the server does not support handling non-ASCII strings.</exception>
		/// <returns>An enumerable collection of unique identifiers (UIDs) which can be used with the
		/// GetMessage family of methods to download the mail messages.</returns>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Search"]/*'/>
		public IEnumerable<uint> Search(SearchCondition criteria, string mailbox = null) {
			AssertValid();
			criteria.ThrowIfNull("criteria");
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag(), str = criteria.ToString();
				StringReader reader = new StringReader(str);
				bool useUTF8 = str.Contains("\r\n");
				string line = reader.ReadLine();
				string response = SendCommandGetResponse(tag + "UID SEARCH " +
					(useUTF8 ? "CHARSET UTF-8 " : "") + line);
				// If our search string consists of multiple lines, we're sending some strings in literal
				// form and need to issue continuation requests.
				while ((line = reader.ReadLine()) != null) {
					if (!response.StartsWith("+")) {
						ResumeIdling();
						throw new NotSupportedException("Please restrict your search " +
							"to ASCII-only characters.", new BadServerResponseException(response));
					}
					response = SendCommandGetResponse(line);
				}
				List<uint> result = new List<uint>();
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"^\* SEARCH (.+)");
					if (m.Success) {
						string[] v = m.Groups[1].Value.Trim().Split(' ');
						foreach (string s in v) {
							try {
								result.Add(Convert.ToUInt32(s));
							} catch(FormatException) { }
						}
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
		/// Retrieves the mail message with the specified unique identifier (UID).
		/// </summary>
		/// <param name="uid">The unique identifier of the mail message to retrieve.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for this message on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An initialized instance of the MailMessage class representing the fetched mail
		/// message.</returns>
		/// <exception cref="BadServerResponseException">The mail message could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage-1"]/*'/>
		public MailMessage GetMessage(uint uid, bool seen = true, string mailbox = null) {
			return GetMessage(uid, FetchOptions.Normal, seen, mailbox);
		}
	
		/// <summary>
		/// Retrieves the mail message with the specified unique identifier (UID) using the specified
		/// fetch-option.
		/// </summary>
		/// <param name="uid">The unique identifier of the mail message to retrieve.</param>
		/// <param name="options">A value from the FetchOptions enumeration which allows
		/// for fetching selective parts of a mail message.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for this message on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An initialized instance of the MailMessage class representing the fetched mail
		/// message.</returns>
		/// <exception cref="BadServerResponseException">The mail message could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.
		/// <para>If you need more fine-grained control over which parts of a mail message to fetch,
		/// consider using one of the overloaded GetMessage methods.
		/// </para>
		/// </remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage-2"]/*'/>
		public MailMessage GetMessage(uint uid, FetchOptions options,
			bool seen = true, string mailbox = null) {
			AssertValid();
			switch (options) {
				case FetchOptions.HeadersOnly:
					return MessageBuilder.FromHeader(GetMailHeader(uid, seen, mailbox));
				case FetchOptions.NoAttachments:
					return GetMessage(uid, p => { return p.Disposition.Type !=
						ContentDispositionType.Attachment; }, seen, mailbox);
				case FetchOptions.TextOnly:
					return GetMessage(uid, p => { return p.Type == ContentType.Text; },
						seen, mailbox);
				default:
					return MessageBuilder.FromMIME822(GetMessageData(uid, seen, mailbox));
			}
		}

		/// <summary>
		/// Retrieves the mail message with the specified unique identifier (UID) while only fetching
		/// those parts of the message that satisfy the condition of the specified delegate. 
		/// </summary>
		/// <param name="uid">The unique identifier of the mail message to retrieve.</param>
		/// <param name="callback">A delegate which will be invoked for every MIME body-part of the
		/// mail message to determine whether the part should be fetched from the server or
		/// skipped.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for this message on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An initialized instance of the MailMessage class representing the fetched mail
		/// message.</returns>
		/// <exception cref="ArgumentNullException">The callback parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail message could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessage-3"]/*'/>
		public MailMessage GetMessage(uint uid, ExaminePartDelegate callback,
			bool seen = true, string mailbox = null) {
			AssertValid();
			callback.ThrowIfNull("callback");
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string header = GetMailHeader(uid, seen, mailbox);
				MailMessage message = MessageBuilder.FromHeader(header);
				string structure = GetBodystructure(uid, mailbox);
				try {
					Bodypart[] parts = Bodystructure.Parse(structure);
					foreach (Bodypart part in parts) {
						// Let the delegate decide whether the part should be fetched or not.
						if (callback(part) == true) {
							string content = GetBodypart(uid, part.PartNumber, seen, mailbox);
							message.AddBodypart(part, content);
						}
					}
				} catch (FormatException) {
					throw new BadServerResponseException("The server returned an erroneous " +
						"body structure:" + structure);
				}
				ResumeIdling();
				return message;
			}
		}

		/// <summary>
		/// Retrieves the set of mail messages with the specified unique identifiers (UIDs).
		/// </summary>
		/// <param name="uids">An enumerable collection of unique identifiers of the mail messages to
		/// retrieve.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched messages on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An enumerable collection of initialized instances of the MailMessage class
		/// representing the fetched mail messages.</returns>
		/// <exception cref="ArgumentNullException">The uids parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages-1"]/*'/>
		public IEnumerable<MailMessage> GetMessages(IEnumerable<uint> uids, bool seen = true,
			string mailbox = null) {
			return GetMessages(uids, FetchOptions.Normal, seen, mailbox);
		}

		/// <summary>
		/// Retrieves the set of mail messages with the specified unique identifiers (UIDs) while only
		/// fetching those parts of the messages that satisfy the condition of the specified delegate. 
		/// </summary>
		/// <param name="uids">An enumerable collection of unique identifiers of the mail messages to
		/// retrieve.</param>
		/// <param name="callback">A delegate which will be invoked for every MIME body-part of each
		/// mail message to determine whether the part should be fetched from the server or
		/// skipped.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched messages on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An enumerable collection of initialized instances of the MailMessage class
		/// representing the fetched mail messages.</returns>
		/// <exception cref="ArgumentNullException">The uids parameter or the callback parameter is
		/// null.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages-3"]/*'/>
		public IEnumerable<MailMessage> GetMessages(IEnumerable<uint> uids, ExaminePartDelegate callback,
			bool seen = true, string mailbox = null) {
			uids.ThrowIfNull("uids");
			List<MailMessage> list = new List<MailMessage>();
			foreach (uint uid in uids)
				list.Add(GetMessage(uid, callback, seen, mailbox));
			return list;
		}

		/// <summary>
		/// Retrieves the set of mail messages with the specified unique identifiers (UIDs) using the
		/// specified fetch-option.
		/// </summary>
		/// <param name="uids">An enumerable collection of unique identifiers of the mail messages to
		/// retrieve.</param>
		/// <param name="options">A value from the FetchOptions enumeration which allows for fetching
		/// selective parts of a mail message.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched messages on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An enumerable collection of initialized instances of the MailMessage class
		/// representing the fetched mail messages.</returns>
		/// <exception cref="ArgumentNullException">The uids parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMessages-2"]/*'/>
		public IEnumerable<MailMessage> GetMessages(IEnumerable<uint> uids, FetchOptions options,
			bool seen = true, string mailbox = null) {
				uids.ThrowIfNull("uids");
				List<MailMessage> list = new List<MailMessage>();
				foreach (uint uid in uids)
					list.Add(GetMessage(uid, options, seen, mailbox));
				return list;
		}

		/// <summary>
		/// Stores the specified mail message on the IMAP server.
		/// </summary>
		/// <param name="message">The mail message to store on the server.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the message on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the message will be stored in. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to store
		/// the message in.</param>
		/// <returns>The unique identifier (UID) of the stored message.</returns>
		/// <exception cref="ArgumentNullException">The message parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail message could not be stored. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <seealso cref="StoreMessages"/>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="StoreMessage"]/*'/>
		public uint StoreMessage(MailMessage message, bool seen = false, string mailbox = null) {
			AssertValid();
			message.ThrowIfNull("message");
			string mime822 = message.ToMIME822();
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = defaultMailbox;
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "APPEND " +
					Util.UTF7Encode(mailbox).QuoteString() + (seen ? @" (\Seen)" : "") +
					" {" + mime822.Length + "}");
				// The server must send a continuation response before we can go ahead with the actual
				// message data.
				if (!response.StartsWith("+"))
					throw new BadServerResponseException(response);
				response = SendCommandGetResponse(mime822);
				while (response.StartsWith("*"))
					response = GetResponse(); 
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return GetHighestUID(mailbox);
			}
		}

		/// <summary>
		/// Stores the specified mail messages on the IMAP server.
		/// </summary>
		/// <param name="messages">An enumerable collection of mail messages to store on the
		/// server.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for each message on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the messages will be stored in. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to store
		/// the messages in.</param>
		/// <returns>An enumerable collection of unique identifiers (UID) representing the stored
		/// messages on the server.</returns>
		/// <exception cref="ArgumentNullException">The messages parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be stored. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A unique identifier (UID) is a 32-bit value assigned to each message which uniquely
		/// identifies the message within the respective mailbox. No two messages in a mailbox share
		/// the same UID.</remarks>
		/// <seealso cref="StoreMessage"/>
		public IEnumerable<uint> StoreMessages(IEnumerable<MailMessage> messages, bool seen = false,
			string mailbox = null) {
			messages.ThrowIfNull("messages");
			List<uint> list = new List<uint>();
			foreach (MailMessage m in messages)
				list.Add(StoreMessage(m, seen, mailbox));
			return list;
		}

		/// <summary>
		/// Retrieves the mail header for the mail message with the specified unique identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve the mail header for.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched messages on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>A string containing the raw mail header of the mail message with the specified
		/// UID.</returns>
		/// <exception cref="BadServerResponseException">The mail header could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		string GetMailHeader(uint uid, bool seen = true, string mailbox = null) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				StringBuilder builder = new StringBuilder();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid + " (BODY" +
					(seen ? null : ".PEEK") + "[HEADER])", false);
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* \d+ FETCH .* {(\d+)}");
					if (m.Success) {
						int size = Convert.ToInt32(m.Groups[1].Value);
						builder.Append(GetData(size));
						response = GetResponse();
						if (!Regex.IsMatch(response, @"\)\s*$"))
							throw new BadServerResponseException(response);
					}
					response = GetResponse(false);
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return builder.ToString();
			}
		}

		/// <summary>
		/// Retrieves the body structure for the mail message with the specified unique identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve the body structure for.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>A string containing the raw body structure of the mail message with the specified
		/// UID.</returns>
		/// <exception cref="BadServerResponseException">The body structure could not be fetched. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>A body structure is a textual description of the layout of a mail message. It is
		/// described in some detail in RFC 3501 under 7.4.2 FETCH response.</remarks>
		string GetBodystructure(uint uid, string mailbox = null) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid + " (BODYSTRUCTURE)");
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
		/// Retrieves the MIME body-part with the specified part number of the multipart message with
		/// the specified unique identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve a MIME body part for.</param>
		/// <param name="partNumber">The part number of the body part to fetch.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched messages on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the messages will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>A string containing the specified body part of the mail message with the specified
		/// UID.</returns>
		/// <exception cref="BadServerResponseException">The body part could not be fetched. The message
		/// property of the exception contains the error message returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		string GetBodypart(uint uid, string partNumber, bool seen = true,
			string mailbox = null) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				StringBuilder builder = new StringBuilder();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid +
					" (BODY" + (seen ? null : ".PEEK") + "[" + partNumber + "])", false);
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* \d+ FETCH .* {(\d+)}");
					if (m.Success) {
						int size = Convert.ToInt32(m.Groups[1].Value);
						builder.Append(GetData(size));
						response = GetResponse();
						if (!Regex.IsMatch(response, @"\)\s*$"))
							throw new BadServerResponseException(response);
					} else {
						// Some servers inline the data in the FETCH response line.
						m = Regex.Match(response, "\\* \\d+ FETCH \\(.*\"(.*)\".*\\)");
						if (m.Success)
							builder.Append(m.Groups[1]);
					}
					response = GetResponse(false);
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return builder.ToString();
			}
		}

		/// <summary>
		/// Retrieves the raw MIME/RFC822 mail message data for the mail message with the specified UID.
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve as a MIME/RFC822 string.</param>
		/// <param name="seen">Set this to true to set the \Seen flag for the fetched message on the
		/// server.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>A string containing the raw MIME/RFC822 data of the mail message with the
		/// specified UID.</returns>
		/// <exception cref="BadServerResponseException">The mail message data could not be fetched.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		string GetMessageData(uint uid, bool seen = true, string mailbox = null) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				StringBuilder builder = new StringBuilder();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid +
					" (BODY" + (seen ? null : ".PEEK") + "[])", false);
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"\* \d+ FETCH .* {(\d+)}");
					if (m.Success) {
						int size = Convert.ToInt32(m.Groups[1].Value);
						builder.Append(GetData(size));
						response = GetResponse();
						if (!Regex.IsMatch(response, @"\)\s*$"))
							throw new BadServerResponseException(response);
					}
					response = GetResponse(false);
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return builder.ToString();
			}
		}

		/// <summary>
		/// Retrieves the highest UID in the specified mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to find the highest UID for. If this parameter is null,
		/// the value of the DefaultMailbox property is used to determine the mailbox to operate
		/// on.</param>
		/// <returns>The highest unique identifier value (UID) in the mailbox.</returns>
		/// <exception cref="BadServerResponseException">The UID could not be determined. The message
		/// property of the exception contains the error message returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>The highest UID usually corresponds to the newest message in a mailbox.</remarks>
		uint GetHighestUID(string mailbox = null) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "STATUS " +
					Util.UTF7Encode(selectedMailbox).QuoteString() + " (UIDNEXT)");
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
		/// Copies the mail message with the specified UID to the specified destination mailbox.
		/// </summary>
		/// <param name="uid">The UID of the mail message to copy.</param>
		/// <param name="destination">The name of the mailbox to copy the message to.</param>
		/// <param name="mailbox">The mailbox the message will be copied from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <exception cref="ArgumentNullException">The destination parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail message could not be copied to the
		/// specified destination. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="MoveMessage"/>
		public void CopyMessage(uint uid, string destination, string mailbox = null) {
			CopyMessages(new HashSet<uint>() { uid }, destination, mailbox);
		}

		/// <summary>
		/// Copies the mail messages with the specified UIDs to the specified destination mailbox.
		/// </summary>
		/// <param name="uids">An enumerable collection of UIDs of the mail messages to copy.</param>
		/// <param name="destination">The name of the mailbox to copy the messages to.</param>
		/// <param name="mailbox">The mailbox the message will be copied from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <remarks>When copying many messages, this method is more efficient than calling
		/// <see cref="CopyMessage"/> for each individual message.</remarks>
		/// <exception cref="ArgumentNullException">The uids parameter or the destination parameter is
		/// null.</exception>
		/// <exception cref="ArgumentException">The specified collection of UIDs is empty.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be copied to the
		/// specified destination. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="MoveMessages"/>
		public void CopyMessages(IEnumerable<uint> uids, string destination, string mailbox = null) {
			AssertValid();
			uids.ThrowIfNull("uids");
			destination.ThrowIfNull("destination");
			string set = Util.BuildSequenceSet(uids);
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID COPY " + set + " " +
					destination.QuoteString());
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Moves the mail message with the specified UID to the specified destination mailbox.
		/// </summary>
		/// <param name="uid">The UID of the mail message to move.</param>
		/// <param name="destination">The name of the mailbox to move the message into.</param>
		/// <param name="mailbox">The mailbox the message will be moved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <exception cref="ArgumentNullException">The destination parameter is null.</exception>
		/// <exception cref="BadServerResponseException">The mail message could not be moved to the
		/// specified destination. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="CopyMessage"/>
		/// <seealso cref="DeleteMessage"/>
		public void MoveMessage(uint uid, string destination, string mailbox = null) {
			CopyMessage(uid, destination, mailbox);
			DeleteMessage(uid, mailbox);
		}

		/// <summary>
		/// Moves the mail messages with the specified UIDs to the specified destination mailbox.
		/// </summary>
		/// <param name="uids">An enumerable collection of UIDs of the mail messages to move.</param>
		/// <param name="destination">The name of the mailbox to move the messages into.</param>
		/// <param name="mailbox">The mailbox the messages will be moved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <remarks>When moving many messages, this method is more efficient than calling
		/// <see cref="MoveMessage"/> for each individual message.</remarks>
		/// <exception cref="ArgumentNullException">The uids parameter or the destination parameter is
		/// null.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be moved to the
		/// specified destination. The message property of the exception contains the error message
		/// returned by the server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="CopyMessages"/>
		/// <seealso cref="DeleteMessages"/>
		public void MoveMessages(IEnumerable<uint> uids, string destination, string mailbox = null) {
			CopyMessages(uids, destination, mailbox);
			DeleteMessages(uids, mailbox);
		}

		/// <summary>
		/// Deletes the mail message with the specified UID.
		/// </summary>
		/// <param name="uid">The UID of the mail message to delete.</param>
		/// <param name="mailbox">The mailbox the message will be deleted from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <exception cref="BadServerResponseException">The mail message could not be deleted. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="MoveMessage"/>
		public void DeleteMessage(uint uid, string mailbox = null) {
			DeleteMessages(new HashSet<uint>() { uid }, mailbox);
		}

		/// <summary>
		/// Deletes the mail messages with the specified UIDs.
		/// </summary>
		/// <param name="uids">An enumerable collection of UIDs of the mail messages to delete.</param>
		/// <param name="mailbox">The mailbox the messages will be deleted from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <remarks>When deleting many messages, this method is more efficient than calling
		/// <see cref="DeleteMessage"/> for each individual message.</remarks>
		/// <exception cref="ArgumentNullException">The uids parameter is null.</exception>
		/// <exception cref="ArgumentException">The specified collection of UIDs is empty.</exception>
		/// <exception cref="BadServerResponseException">The mail messages could not be deleted. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="MoveMessages"/>
		public void DeleteMessages(IEnumerable<uint> uids, string mailbox = null) {
			AssertValid();
			uids.ThrowIfNull("uids");
			string set = Util.BuildSequenceSet(uids);
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + set +
					@" +FLAGS.SILENT (\Deleted \Seen)");
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Retrieves the IMAP message flag attributes for the mail message with the specified unique
		/// identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to retrieve the flag attributes for.</param>
		/// <param name="mailbox">The mailbox the message will be retrieved from. If this parameter is
		/// omitted, the value of the DefaultMailbox property is used to determine the mailbox to
		/// operate on.</param>
		/// <returns>An enumerable collection of message flags set for the message with the specified
		/// UID.</returns>
		/// <exception cref="BadServerResponseException">The mail message flags could not be retrieved.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="SetMessageFlags"/>
		/// <seealso cref="AddMessageFlags"/>
		/// <seealso cref="RemoveMessageFlags"/>
		public IEnumerable<MessageFlag> GetMessageFlags(uint uid, string mailbox = null) {
			Dictionary<string, MessageFlag> messageFlagsMapping =
			new Dictionary<string, MessageFlag>(StringComparer.OrdinalIgnoreCase) {
				{ @"\Seen", MessageFlag.Seen },
				{ @"\Answered", MessageFlag.Answered },
				{ @"\Flagged", MessageFlag.Flagged },
				{ @"\Deleted", MessageFlag.Deleted },
				{ @"\Draft", MessageFlag.Draft },
				{ @"\Recent",	MessageFlag.Recent }
			};
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID FETCH " + uid + " (FLAGS)");
				List<MessageFlag> flags = new List<MessageFlag>();
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, @"FLAGS \(([\w\s\\$-]*)\)");
					if (m.Success) {
						string[] setFlags = m.Groups[1].Value.Split(' ');
						foreach (string flag in setFlags) {
							if (messageFlagsMapping.ContainsKey(flag))
								flags.Add(messageFlagsMapping[flag]);
						}
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return flags;
			}
		}

		/// <summary>
		/// Sets the IMAP message flag attributes for the mail message with the specified unique
		/// identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to set the flag attributes for.</param>
		/// <param name="mailbox">The mailbox that contains the mail message. If this parameter is null,
		/// the value of the DefaultMailbox property is used to determine the mailbox to operate
		/// on.</param>
		/// <param name="flags">One or multiple message flags from the MessageFlag enumeration.</param>
		/// <exception cref="BadServerResponseException">The mail message flags could not be set. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>This method replaces the current flag attributes of the message with the specified
		/// new flags. If you wish to retain the old attributes, use the <see cref="AddMessageFlags"/>
		/// method instead.</remarks>
		/// <seealso cref="GetMessageFlags"/>
		/// <seealso cref="AddMessageFlags"/>
		/// <seealso cref="RemoveMessageFlags"/>
		public void SetMessageFlags(uint uid, string mailbox, params MessageFlag[] flags) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string flagsString = "";
				foreach (MessageFlag f in flags)
					flagsString = flagsString + @"\" + f + " ";
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" FLAGS.SILENT (" + flagsString.Trim() + ")");
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Adds the specified set of IMAP message flags to the existing flag attributes of the mail
		/// message with the specified unique identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to add the flag attributes to.</param>
		/// <param name="mailbox">The mailbox that contains the mail message. If this parameter is null,
		/// the value of the DefaultMailbox property is used to determine the mailbox to operate
		/// on.</param>
		/// <param name="flags">One or multiple message flags from the MessageFlag  enumeration.</param>
		/// <exception cref="BadServerResponseException">The mail message flags could not be added. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>This method adds the specified set of flags to the existing set of flag attributes
		/// of the message. If you wish to replace the old attributes, use the
		/// <see cref="SetMessageFlags"/> method instead.</remarks>
		/// <seealso cref="GetMessageFlags"/>
		/// <seealso cref="SetMessageFlags"/>
		/// <seealso cref="RemoveMessageFlags"/>
		public void AddMessageFlags(uint uid, string mailbox, params MessageFlag[] flags) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string flagsString = "";
				foreach (MessageFlag f in flags)
					flagsString = flagsString + @"\" + f + " ";
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" +FLAGS.SILENT (" + flagsString.Trim() + ")");
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Removes the specified set of IMAP message flags from the existing flag attributes of the
		/// mail message with the specified unique identifier (UID).
		/// </summary>
		/// <param name="uid">The UID of the mail message to remove the flag attributes for.</param>
		/// <param name="mailbox">The mailbox that contains the mail message. If this parameter is null,
		/// the value of the DefaultMailbox property is used to determine the mailbox to operate
		/// on.</param>
		/// <param name="flags">One or multiple message flags from the MessageFlag  enumeration.</param>
		/// <exception cref="BadServerResponseException">The mail message flags could not be removed.
		/// The message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>This method removes the specified set of flags from the existing set of flag
		/// attributes of the message. If you wish to replace the old attributes, use the
		/// <see cref="SetMessageFlags"/> method instead.</remarks>
		/// <seealso cref="GetMessageFlags"/>
		/// <seealso cref="SetMessageFlags"/>
		/// <seealso cref="AddMessageFlags"/>
		public void RemoveMessageFlags(uint uid, string mailbox, params MessageFlag[] flags) {
			AssertValid();
			lock (sequenceLock) {
				PauseIdling();
				SelectMailbox(mailbox);
				string flagsString = "";
				foreach (MessageFlag f in flags)
					flagsString = flagsString + @"\" + f + " ";
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "UID STORE " + uid +
					@" -FLAGS.SILENT (" + flagsString.Trim() + ")");
				while (response.StartsWith("*"))
					response = GetResponse();
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
			}
		}

		/// <summary>
		/// Starts receiving of IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="ApplicationException">An unexpected program condition occured.</exception>
		/// <exception cref="BadServerResponseException">The IDLE operation could not be completed. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="InvalidOperationException">The server does not support the IMAP4 IDLE
		/// command.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>Calling this method when already receiving idle notifications has no
		/// effect.</remarks>
		/// <seealso cref="StopIdling"/>
		/// <seealso cref="PauseIdling"/>
		/// <seealso cref="ResumeIdling"/>
		void StartIdling() {
			if (idling)
				return;
			if (!Supports("IDLE"))
				throw new InvalidOperationException("The server does not support the IMAP4 IDLE command.");
			lock (sequenceLock) {
				// Make sure the default mailbox is selected.
				SelectMailbox(null);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "IDLE");
				// The server must respond with a continuation response.
				if (!response.StartsWith("+"))
					throw new BadServerResponseException(response);
			}
			// Setup and start the idle thread.
			if (idleThread != null)
				throw new ApplicationException("idleThread is not null.");
			idling = true;
			idleThread = new Thread(IdleLoop);
			idleThread.IsBackground = true;
			idleThread.Start();
			// Setup a timer to issue NOOPs every once in a while.
			noopTimer.Interval = 1000 * 60 * 10;
			noopTimer.Elapsed += IssueNoop;
			noopTimer.Start();
		}

		/// <summary>
		/// Stops receiving IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="BadServerResponseException">The IDLE operation could not be completed. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="InvalidOperationException">The server does not support the IMAP4 IDLE
		/// command.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>Calling this method when not receiving idle notifications has no effect.</remarks>
		/// <seealso cref="StartIdling"/>
		/// <seealso cref="PauseIdling"/>
		void StopIdling() {
			AssertValid();
			if (!idling)
				return;
			SendCommand("DONE");
			// Wait until the idle thread has shutdown.
			idleThread.Join();
			idleThread = null;
			idling = false;
			noopTimer.Stop();
		}

		/// <summary>
		/// Temporarily pauses receiving IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="BadServerResponseException">The IDLE operation could not be completed. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="InvalidOperationException">The server does not support the IMAP4 IDLE
		/// command.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <remarks>To resume receiving IDLE notifications ResumeIdling must be called.
		/// </remarks>
		/// <seealso cref="StartIdling"/>
		/// <seealso cref="ResumeIdling"/>
		void PauseIdling() {
			AssertValid();
			if (!idling)
				return;
			pauseRefCount = pauseRefCount + 1;
			if (pauseRefCount != 1)
				return;
			// Send a "DONE" continuation-command to indicate we no longer want to receive idle
			// notifications. The server response is consumed by the idle thread and signals it to
			// shut down.
			SendCommand("DONE");

			// Wait until the idle thread has shutdown.
			idleThread.Join();
			idleThread = null;
		}

		/// <summary>
		/// Resumes receiving IMAP IDLE notifications from the IMAP server.
		/// </summary>
		/// <exception cref="ApplicationException">An unexpected program condition occured.</exception>
		/// <exception cref="BadServerResponseException">The IDLE operation could not be completed. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="InvalidOperationException">The server does not support the IMAP4 IDLE
		/// command.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		/// <seealso cref="StopIdling"/>
		void ResumeIdling() {
			AssertValid();
			if (!idling)
				return;
			pauseRefCount = pauseRefCount - 1;
			if (pauseRefCount != 0)
				return;
			// Make sure the default mailbox is selected.
			lock (sequenceLock) {
				SelectMailbox(null);
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "IDLE");
				// The server must respond with a continuation response.
				if (!response.StartsWith("+"))
					throw new BadServerResponseException(response);
			}
			// Setup and start the idle thread.
			if (idleThread != null)
				throw new ApplicationException("idleThread is not null.");
			idleThread = new Thread(IdleLoop);
			idleThread.IsBackground = true;
			idleThread.Start();
		}

		/// <summary>
		/// The main idle loop. Waits for incoming IMAP IDLE notifications and dispatches
		/// them as events. This runs in its own thread whenever IMAP IDLE
		/// notifications are being received.
		/// </summary>
		void IdleLoop() {
			if (idleDispatch == null) {
				idleDispatch = new Thread(EventDispatcher);
				idleDispatch.IsBackground = true;
				idleDispatch.Start();
			}

			while (true) {
				try {
					string response = GetResponse();
					// A request was made to stop idling so quit the thread.
					if (response.Contains("OK IDLE", StringComparison.InvariantCultureIgnoreCase))
						return;
					// Let the dispatcher thread take care of the IDLE notification so we can go back to
					// receiving responses.
					idleEvents.Enqueue(response);
				} catch (IOException e) {
					// Closing _Stream or the underlying _Connection instance will cause a
					// WSACancelBlockingCall exception on a blocking socket. This is not an error so just let
					// it pass.
					if (e.InnerException is SocketException) {
						// WSAEINTR = 10004
						if (((SocketException)e.InnerException).ErrorCode == 10004)
							return;
					}
					// If the IO exception was raised because of an underlying ThreadAbortException, we can
					// ignore it.
					if (e.InnerException is ThreadAbortException)
						return;
					// Otherwise shutdown and raise the IdleError event to let the user know something
					// went wrong.
					idleThread = null;
					idling = false;
					noopTimer.Stop();
					try {
						IdleError.Raise(this, new IdleErrorEventArgs(e, this));
					} catch {
					}
					return;
				}
			}
		}

		/// <summary>
		/// Blocks on a queue and wakes up whenever a new notification is put into the queue. The
		/// notification is then examined and dispatched as an event.
		/// </summary>
		void EventDispatcher() {
			uint lastUid = 0;
			while (true) {
				string response = idleEvents.Dequeue();
				Match m = Regex.Match(response, @"\*\s+(\d+)\s+(\w+)");
				if (!m.Success)
					continue;
				try {
					uint numberOfMessages = Convert.ToUInt32(m.Groups[1].Value),
						uid = GetHighestUID();
					switch (m.Groups[2].Value.ToUpper()) {
						case "EXISTS":
							if (lastUid != uid) {
								newMessageEvent.Raise(this,
									new IdleMessageEventArgs(numberOfMessages, uid, this));
							}
							break;
						case "EXPUNGE":
							messageDeleteEvent.Raise(
								this, new IdleMessageEventArgs(numberOfMessages, uid, this));
							break;
					}
					lastUid = uid;
				} catch {
					// Fall through.
				}
			}
		}

		/// <summary>
		/// Issues a NOOP command to the IMAP server. Called in the context of a System.Timer event
		/// when IDLE notifications are being received.
		/// </summary>
		/// <remarks>This is needed by the IMAP IDLE mechanism to give the server an indication that the
		/// connection is still active.
		/// </remarks>
		void IssueNoop(object sender, ElapsedEventArgs e) {
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
		/// Retrieves IMAP QUOTA information for the specified mailbox.
		/// </summary>
		/// <param name="mailbox">The mailbox to retrieve QUOTA information for. If this parameter is
		/// null, the value of the DefaultMailbox property is used to determine the mailbox to operate
		/// on.</param>
		/// <returns>An enumerable collection of MailboxQuota objects describing usage and limits of the
		/// quota roots for the mailbox.</returns>
		/// <exception cref="BadServerResponseException">The quota operation could not be completed. The
		/// message property of the exception contains the error message returned by the
		/// server.</exception>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been disposed.</exception>
		/// <exception cref="IOException">There was a failure writing to or reading from the
		/// network.</exception>
		/// <exception cref="InvalidOperationException">The IMAP4 QUOTA extension is not supported by
		/// the server.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		IEnumerable<MailboxQuota> GetQuota(string mailbox = null) {
			AssertValid();
			if (!Supports("QUOTA"))
				throw new InvalidOperationException(
					"This server does not support the IMAP4 QUOTA extension.");
			lock (sequenceLock) {
				PauseIdling();
				if (mailbox == null)
					mailbox = DefaultMailbox;
				List<MailboxQuota> quotas = new List<MailboxQuota>();
				string tag = GetTag();
				string response = SendCommandGetResponse(tag + "GETQUOTAROOT " +
					Util.UTF7Encode(mailbox).QuoteString());
				while (response.StartsWith("*")) {
					Match m = Regex.Match(response, "\\* QUOTA \"(\\w*)\" \\((\\w+)\\s+(\\d+)\\s+(\\d+)\\)");
					if (m.Success) {
						try {
							MailboxQuota quota = new MailboxQuota(m.Groups[2].Value,
								UInt32.Parse(m.Groups[3].Value),
								UInt32.Parse(m.Groups[4].Value));
							quotas.Add(quota);
						} catch {
							throw new BadServerResponseException(response);
						}
					}
					response = GetResponse();
				}
				ResumeIdling();
				if (!IsResponseOK(response, tag))
					throw new BadServerResponseException(response);
				return quotas;
			}
		}

		/// <summary>
		/// Releases all resources used by the current instance of the ImapClient class.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases all resources used by the current instance of the ImapClient class, optionally
		/// disposing of managed resource.
		/// </summary>
		/// <param name="disposing">true to dispose of managed resources, otherwise false.</param>
		protected virtual void Dispose(bool disposing) {
			if (!disposed) {
				// Indicate that the instance has been disposed.
				disposed = true;
				// Get rid of managed resources.
				if (disposing) {
					if (idleThread != null) {
						idleThread.Abort();
						idleThread = null;
					}
					if (idleDispatch != null) {
						idleDispatch.Abort();
						idleDispatch = null;
					}
					noopTimer.Stop();
					stream.Close();
					stream = null;
					if (client != null)
						client.Close();
					client = null;
				}
				// Get rid of unmanaged resources.
			}
		}

		/// <summary>
		/// Asserts the instance has not been disposed of and is in a valid state.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The ImapClient object has been
		/// disposed.</exception>
		/// <exception cref="NotAuthenticatedException">The method was called in non-authenticated
		/// state, i.e. before logging in.</exception>
		void AssertValid(bool requireAuth = true) {
			if (disposed)
				throw new ObjectDisposedException(GetType().FullName);
			if(requireAuth && !Authed)
					throw new NotAuthenticatedException();
		}
	}

	/// <summary>
	/// A delegate which is invoked during a call to GetMessage or GetMessages for every MIME part in
	/// a multipart mail message. The delegate can examine the MIME body part and decide to either
	/// include it in the returned mail message or dismiss it.
	/// </summary>
	/// <param name="part">A MIME body part of a mail message which consists of multiple parts.</param>
	/// <returns>true to include the body part in the returned MailMessage object, or false to skip
	/// it.</returns>
	public delegate bool ExaminePartDelegate(Bodypart part);
}