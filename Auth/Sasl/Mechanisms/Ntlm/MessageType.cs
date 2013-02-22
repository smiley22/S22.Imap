
namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Describes the different types of NTLM messages.
	/// </summary>
	internal enum MessageType {
		/// <summary>
		/// An NTLM type 1 message is the initial client response to the
		/// server.
		/// </summary>
		Type1 = 0x01,
		/// <summary>
		/// An NTLM type 2 message is the challenge sent by the server in
		/// response to an NTLM type 1 message.
		/// </summary>
		Type2 = 0x02,
		/// <summary>
		/// An NTLM type 3 message is the challenge response sent by the client
		/// in response to an NTLM type 2 message.
		/// </summary>
		Type3 = 0x03
	}
}
