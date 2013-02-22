using S22.Imap.Auth;

namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Indicates the version and build number of the operating system.
	/// </summary>
	internal class OSVersion {
		/// <summary>
		/// The major version number of the operating system.
		/// </summary>
		public byte MajorVersion {
			get;
			set;
		}

		/// <summary>
		/// The minor version number of the operating system.
		/// </summary>
		public byte MinorVersion {
			get;
			set;
		}

		/// <summary>
		/// The build number of the operating system.
		/// </summary>
		public short BuildNumber {
			get;
			set;
		}

		/// <summary>
		/// Default constructor.
		/// </summary>
		public OSVersion() {
		}

		/// <summary>
		/// Creates a new instance of the OSVersion class using the specified
		/// values.
		/// </summary>
		/// <param name="majorVersion">The major version of the operating
		/// system.</param>
		/// <param name="minorVersion">The minor version of the operating
		/// system.</param>
		/// <param name="buildNumber">The build number of the operating systen.</param>
		public OSVersion(byte majorVersion, byte minorVersion, short buildNumber) {
			MajorVersion = majorVersion;
			MinorVersion = minorVersion;
			BuildNumber = buildNumber;
		}

		/// <summary>
		/// Serializes this instance of the OSVersion class to an array of
		/// bytes.
		/// </summary>
		/// <returns>An array of bytes representing this instance of the OSVersion
		/// class.</returns>
		public byte[] Serialize() {
			return new ByteBuilder()
				.Append(MajorVersion)
				.Append(MinorVersion)
				.Append(BuildNumber)
				.Append(0, 0, 0, 0x0F)
				.ToArray();
		}
	}
}
