
namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Represents the data contained in the target information block of an
	/// NTLM type 2 message.
	/// </summary>
	internal class Type2TargetInformation {
		/// <summary>
		/// The server name.
		/// </summary>
		public string ServerName {
			get;
			set;
		}

		/// <summary>
		/// The domain name.
		/// </summary>
		public string DomainName {
			get;
			set;
		}

		/// <summary>
		/// The fully-qualified DNS host name.
		/// </summary>
		public string DnsHostname {
			get;
			set;
		}

		/// <summary>
		/// The fully-qualified DNS domain name.
		/// </summary>
		public string DnsDomainName {
			get;
			set;
		}
	}

	/// <summary>
	/// Describes the different versions of the Type 2 message that have
	/// been observed.
	/// </summary>
	internal enum Type2Version {
		/// <summary>
		/// The version is unknown.
		/// </summary>
		Unknown = 0,
		/// <summary>
		/// This form is seen in older Win9x-based systems.
		/// </summary>
		Version1 = 32,
		/// <summary>
		/// This form is seen in most out-of-box shipping versions of Windows.
		/// </summary>
		Version2 = 48,
		/// <summary>
		/// This form was introduced in a relatively recent Service Pack, and
		/// is seen on currently-patched versions of Windows 2000, Windows XP,
		/// and Windows 2003.
		/// </summary>
		Version3 = 56,
	}

	/// <summary>
	/// Indicates the type of data in Type 2 target information blocks.
	/// </summary>
	internal enum Type2InformationType {
		/// <summary>
		/// Signals the end of the target information block.
		/// </summary>
		TerminatorBlock = 0,
		/// <summary>
		/// The data in the information block contains the server name.
		/// </summary>
		ServerName = 1,
		/// <summary>
		/// The data in the information block contains the domain name.
		/// </summary>
		DomainName = 2,
		/// <summary>
		/// The data in the information block contains the DNS hostname.
		/// </summary>
		DnsHostname = 3,
		/// <summary>
		/// The data in the information block contans the DNS domain name.
		/// </summary>
		DnsDomainName = 4
	}
}
