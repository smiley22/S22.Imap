using System;
using System.Net.Mail;

namespace S22.Imap
{
    public interface IImapClient: IDisposable
    {
        /// <summary>
        /// The default mailbox to operate on, when no specific mailbox name was indicated
        /// to methods operating on mailboxes. This property is initially set to "INBOX".
        /// </summary>
        /// <exception cref="ArgumentNullException">The value specified for a set operation is
        /// null.</exception>
        /// <exception cref="ArgumentException">The value specified for a set operation is equal
        /// to String.Empty ("").</exception>
        /// <remarks>This property is initialized to "INBOX"</remarks>
        string DefaultMailbox { get; set; }

        /// <summary>
        /// Indicates whether the client is authenticated with the server
        /// </summary>
        bool Authed { get; }

        /// <summary>
        /// This event is raised when a new mail message is received by the server.
        /// </summary>
        /// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
        /// method can be used, specifying "IDLE" for the capability parameter.
        /// 
        /// Notice that the event handler will be executed on a threadpool thread.
        /// </remarks>
        /// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="NewMessage"]/*'/>
        event EventHandler<IdleMessageEventArgs> NewMessage;

        /// <summary>
        /// This event is raised when a message is deleted on the server.
        /// </summary>
        /// <remarks>To probe a server for IMAP IDLE support, the <see cref="Supports"/>
        /// method can be used, specifying "IDLE" for the capability parameter.
        /// 
        /// Notice that the event handler will be executed on a threadpool thread.
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
        /// <exception cref="InvalidCredentialsException">Thrown if authentication using the
        /// supplied credentials failed.</exception>
        /// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Login"]/*'/>
        void Login(string username, string password, AuthMethod method);

        /// <summary>
        /// Logs an authenticated client out of the server. After the logout sequence has
        /// been completed, the server closes the connection with the client.
        /// </summary>
        /// <exception cref="BadServerResponseException">Thrown if an unexpected response is
        /// received from the server during the logout sequence</exception>
        /// <remarks>Calling Logout in a non-authenticated state has no effect</remarks>
        void Logout();

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
        string[] Capabilities();

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
        bool Supports(string capability);

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
        void RenameMailbox(string mailbox, string newName);

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
        void DeleteMailbox(string mailbox);

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
        void CreateMailbox(string mailbox);

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
        string[] ListMailboxes();

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
        void Expunge(string mailbox = null);

        /// <summary>
        /// Retrieves status information (total number of messages, various attributes
        /// as well as quota information) for the specified mailbox.</summary>
        /// <param name="mailbox">The mailbox to retrieve status information for. If this
        /// parameter is omitted, the value of the DefaultMailbox property is used to
        /// determine the mailbox to operate on.</param>
        /// <returns>A MailboxInfo object containing status information for the
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
        /// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="GetMailboxInfo"]/*'/>
        MailboxInfo GetMailboxInfo(string mailbox = null);

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
        /// <exception cref="NotSupportedException">Thrown if the search values
        /// contain characters beyond the ASCII range and the server does not support
        /// handling such strings.</exception>
        /// <returns>An array of unique identifier (UID) message attributes which
        /// can be used with the GetMessage family of methods to download mail
        /// messages.</returns>
        /// <remarks>A unique identifier (UID) is a 32-bit value assigned to each
        /// message which uniquely identifies the message within a mailbox. No two
        /// messages in a mailbox share the the same UID.</remarks>
        /// <include file='Examples.xml' path='S22/Imap/ImapClient[@name="Search"]/*'/>
        uint[] Search(SearchCondition criteria, string mailbox = null);

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
        MailMessage GetMessage(uint uid, bool seen = true, string mailbox = null);

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
        MailMessage GetMessage(uint uid, FetchOptions options,
                                               bool seen = true, string mailbox = null);

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
        MailMessage GetMessage(uint uid, ExaminePartDelegate callback,
                                               bool seen = true, string mailbox = null);

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
        MailMessage[] GetMessages(uint[] uids, bool seen = true, string mailbox = null);

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
        MailMessage[] GetMessages(uint[] uids, ExaminePartDelegate callback,
                                                  bool seen = true, string mailbox = null);

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
        MailMessage[] GetMessages(uint[] uids, FetchOptions options,
                                                  bool seen = true, string mailbox = null);

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
        uint StoreMessage(MailMessage message, bool seen = false, string mailbox = null);

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
        uint[] StoreMessages(MailMessage[] messages, bool seen = false,
                                             string mailbox = null);

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
        void CopyMessage(uint uid, string destination, string mailbox = null);

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
        void MoveMessage(uint uid, string destination, string mailbox = null);

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
        void DeleteMessage(uint uid, string mailbox = null);

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
        MessageFlag[] GetMessageFlags(uint uid, string mailbox = null);

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
        void SetMessageFlags(uint uid, string mailbox, params MessageFlag[] flags);

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
        void AddMessageFlags(uint uid, string mailbox, params MessageFlag[] flags);

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
        void RemoveMessageFlags(uint uid, string mailbox, params MessageFlag[] flags);
    }
}