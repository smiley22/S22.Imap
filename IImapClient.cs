using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Mail;

namespace S22.Imap {
	/// <summary>
	/// Enables applications to communicate with a mail server using the Internet Message Access
	/// Protocol (IMAP).
	/// </summary>
	public interface IImapClient : IDisposable {
		/// <summary>
		/// The default mailbox to operate on.
		/// </summary>
		/// <exception cref="ArgumentNullException">The property is being set and the value is
		/// null.</exception>
		/// <exception cref="ArgumentException">The property is being set and the value is the empty
		/// string.</exception>
		/// <remarks>The default value for this property is "INBOX" which is a special name reserved
		/// to mean "the primary mailbox for this user on this server".</remarks>
		string DefaultMailbox { get; set; }

		/// <summary>
		/// Determines whether the client is authenticated with the server.
		/// </summary>
		bool Authed { get; }

		/// <summary>
		/// The event that is raised when a new mail message has been received by the server.
		/// </summary>
		/// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
		/// method can be used, specifying "IDLE" for the capability parameter.
		/// 
		/// Please note that the event handler will be executed on a threadpool thread.
		/// </remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="NewMessage"]/*'/>
		event EventHandler<IdleMessageEventArgs> NewMessage;

		/// <summary>
		/// The event that is raised when a message has been deleted on the server.
		/// </summary>
		/// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
		/// method can be used, specifying "IDLE" for the capability parameter.
		/// 
		/// Please note that the event handler will be executed on a threadpool thread.
		/// </remarks>
		/// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="MessageDeleted"]/*'/>
		event EventHandler<IdleMessageEventArgs> MessageDeleted;

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
		void Login(string username, string password, AuthMethod method);

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
		void Logout();

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
		IEnumerable<string> Capabilities();

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
		bool Supports(string capability);

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
		void RenameMailbox(string mailbox, string newName);

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
		void DeleteMailbox(string mailbox);

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
		void CreateMailbox(string mailbox);

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
		IEnumerable<string> ListMailboxes();

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
		void Expunge(string mailbox = null);

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
		MailboxInfo GetMailboxInfo(string mailbox = null);

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
		IEnumerable<uint> Search(SearchCondition criteria, string mailbox = null);

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
		MailMessage GetMessage(uint uid, bool seen = true, string mailbox = null);

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
		MailMessage GetMessage(uint uid, FetchOptions options, bool seen = true, string mailbox = null);

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
		MailMessage GetMessage(uint uid, ExaminePartDelegate callback, bool seen = true,
			string mailbox = null);

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
		IEnumerable<MailMessage> GetMessages(IEnumerable<uint> uids, bool seen = true,
			string mailbox = null);

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
		IEnumerable<MailMessage> GetMessages(IEnumerable<uint> uids, ExaminePartDelegate callback,
			bool seen = true, string mailbox = null);

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
		IEnumerable<MailMessage> GetMessages(IEnumerable<uint> uids, FetchOptions options,
			bool seen = true, string mailbox = null);

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
		uint StoreMessage(MailMessage message, bool seen = false, string mailbox = null);

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
		IEnumerable<uint> StoreMessages(IEnumerable<MailMessage> messages, bool seen = false,
			string mailbox = null);

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
		void CopyMessage(uint uid, string destination, string mailbox = null);

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
		void CopyMessages(IEnumerable<uint> uids, string destination, string mailbox = null);

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
		void MoveMessage(uint uid, string destination, string mailbox = null);

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
		void MoveMessages(IEnumerable<uint> uids, string destination, string mailbox = null);

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
		void DeleteMessage(uint uid, string mailbox = null);

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
		void DeleteMessages(IEnumerable<uint> uids, string mailbox = null);

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
		IEnumerable<MessageFlag> GetMessageFlags(uint uid, string mailbox = null);

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
		void SetMessageFlags(uint uid, string mailbox, params MessageFlag[] flags);

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
		void AddMessageFlags(uint uid, string mailbox, params MessageFlag[] flags);

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
		void RemoveMessageFlags(uint uid, string mailbox, params MessageFlag[] flags);
	}
}