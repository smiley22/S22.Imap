using System;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Represents an NTLM Type 1 Message.
	/// </summary>
	internal class Type1Message {
		/// <summary>
		/// The NTLM message signature which is always "NTLMSSP".
		/// </summary>
		static readonly string signature = "NTLMSSP";

		/// <summary>
		/// The NTML message type which is always 1 for an NTLM Type 1 message.
		/// </summary>
		static readonly MessageType type = MessageType.Type1;

		/// <summary>
		/// The NTLM flags set on this instance.
		/// </summary>
		internal Flags Flags {
			get;
			set;
		}

		/// <summary>
		/// The supplied domain name as an array of bytes in the ASCII
		/// range.
		/// </summary>
		byte[] domain {
			get;
			set;
		}

		/// <summary>
		/// The offset within the message where the domain name data starts.
		/// </summary>
		int domainOffset {
			get {
				// We send a version 3 NTLM type 1 message.
				return 40;
			}
		}

		/// <summary>
		/// The supplied workstation name as an array of bytes in the
		/// ASCII range.
		/// </summary>
		byte[] workstation {
			get;
			set;
		}

		/// <summary>
		/// The offset within the message where the workstation name data starts.
		/// </summary>
		int workstationOffset {
			get {
				return domainOffset + domain.Length;
			}
		}

		/// <summary>
		/// The length of the supplied workstation name as a 16-bit short value.
		/// </summary>
		short workstationLength {
			get {
				return Convert.ToInt16(workstation.Length);
			}
		}

		/// <summary>
		/// Contains information about the client's OS version.
		/// </summary>
		OSVersion OSVersion {
			get;
			set;
		}

		/// <summary>
		/// Creates a new instance of the Type1Message class using the specified
		/// domain and workstation names.
		/// </summary>
		/// <param name="domain">The domain in which the client's workstation has
		/// membership.</param>
		/// <param name="workstation">The client's workstation name.</param>
		/// <exception cref="ArgumentNullException">Thrown if the domain or the
		/// workstation parameter is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if the domain
		/// or the workstation name exceeds the maximum allowed string
		/// length.</exception>
		/// <remarks>The domain as well as the workstation name is restricted
		/// to ASCII characters and must not be longer than 65536 characters.
		/// </remarks>
		public Type1Message(string domain, string workstation) {
			// Fixme: Is domain mandatory?
			domain.ThrowIfNull("domain");
			workstation.ThrowIfNull("workstation");

			this.domain = Encoding.ASCII.GetBytes(domain);
			if (this.domain.Length >= Int16.MaxValue) {
				throw new ArgumentOutOfRangeException("The supplied domain name must " +
					"not be longer than " + Int16.MaxValue);
			}
			this.workstation = Encoding.ASCII.GetBytes(workstation);
			if (this.workstation.Length >= Int16.MaxValue) {
				throw new ArgumentOutOfRangeException("The supplied workstation name " +
					"must not be longer than " + Int16.MaxValue);
			}

			Flags = Flags.NegotiateUnicode | Flags.RequestTarget | Flags.NegotiateNTLM |
				Flags.NegotiateDomainSupplied | Flags.NegotiateWorkstationSupplied;
			// We spoof an OS version of Windows 7 Build 7601.
			OSVersion = new OSVersion(6, 1, 7601);
		}

		/// <summary>
		/// Serializes this instance of the Type1 class to an array of bytes.
		/// </summary>
		/// <returns>An array of bytes representing this instance of the Type1
		/// class.</returns>
		public byte[] Serialize() {
			return new ByteBuilder()
				.Append(signature + "\0")
				.Append((int) type)
				.Append((int) Flags)
				.Append(new SecurityBuffer(domain, domainOffset).Serialize())
				.Append(new SecurityBuffer(workstation, workstationOffset).Serialize())
				.Append(OSVersion.Serialize())
				.Append(domain)
				.Append(workstation)
				.ToArray();
		}
	}
}
