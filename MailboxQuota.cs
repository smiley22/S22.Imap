using System;

namespace S22.Imap {
	/// <summary>
	/// Represents an IMAP QUOTA entry for a resource which typically consists of a resource name,
	/// the current usage of the resource, and the resource limit.
	/// </summary>
	internal class MailboxQuota {
		/// <summary>
		/// Initializes a new instance of the MailboxQuota class with the specified values.
		/// </summary>
		/// <param name="Name">The name of the resource this MailboxQuota instance describes.</param>
		/// <param name="Usage">The current usage of the resource in units of 1024  bytes.</param>
		/// <param name="Limit">The limit of the resource in units of 1024 bytes.</param>
		internal MailboxQuota(string Name, uint Usage, uint Limit) {
			this.ResourceName = Name.ToUpperInvariant();
			this.Usage = Convert.ToUInt64(Usage) * 1024;
			this.Limit = Convert.ToUInt64(Limit) * 1024;
		}

		/// <summary>
		/// The name of the resource this MailboxQuota instance describes.
		/// </summary>
		public string ResourceName {
			get;
			private set;
		}

		/// <summary>
		/// The current usage of the resource this MailboxQuota instance describes, in bytes.
		/// </summary>
		public UInt64 Usage {
			get;
			private set;
		}
		
		/// <summary>
		/// The limit of the resource this MailboxQuota instance describes, in bytes.
		/// </summary>
		public UInt64 Limit {
			get;
			private set;
		}
	}
}
