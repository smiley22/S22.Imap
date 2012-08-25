using System;
using System.Collections.Specialized;

namespace S22.Imap {
	/// <summary>
	/// Represents a part of a MIME multi-part message. Each part consists
	/// of its own content header and a content body.
	/// </summary>
	/// <remarks>The MIMEPart class has been deprecated as of version 2.0
	/// in favor of partial fetching of mail messages.</remarks>
	[Obsolete("Deprecated as of version 2.0 in favor of partial fetching")] 
	internal class MIMEPart {
		/// <summary>
		/// A collection containing the content header information as
		/// key-value pairs.
		/// </summary>
		public NameValueCollection header {
			get;
			set;
		}
		/// <summary>
		/// A string containing the content body of the part.
		/// </summary>
		public string body {
			get;
			set;
		}
	}
}
