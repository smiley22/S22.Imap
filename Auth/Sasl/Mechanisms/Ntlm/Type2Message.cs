using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Represents an NTLM Type 2 Message.
	/// </summary>
	internal class Type2Message {
		/// <summary>
		/// The NTLM message signature which is always "NTLMSSP".
		/// </summary>
		static readonly string signature = "NTLMSSP";

		/// <summary>
		/// The NTML message type which is always 2 for an NTLM Type 2 message.
		/// </summary>
		static readonly MessageType type = MessageType.Type2;

		/// <summary>
		/// The challenge is an 8-byte block of random data.
		/// </summary>
		public byte[] Challenge {
			get;
			private set;
		}

		/// <summary>
		/// The target name of the authentication target.
		/// </summary>
		public string TargetName {
			get;
			private set;
		}

		/// <summary>
		/// The NTLM flags set on this message.
		/// </summary>
		public Flags Flags {
			get;
			private set;
		}

		/// <summary>
		/// The SSPI context handle when a local call is being made,
		/// otherwise null.
		/// </summary>
		public Int64 Context {
			get;
			private set;
		}

		/// <summary>
		/// Contains the data present in the OS version structure.
		/// </summary>
		public OSVersion OSVersion {
			get;
			private set;
		}

		/// <summary>
		/// The version of this Type 2 message instance.
		/// </summary>
		public Type2Version Version {
			get;
			private set;
		}

		/// <summary>
		/// Contains the data present in the target information block.
		/// </summary>
		public Type2TargetInformation TargetInformation {
			get;
			private set;
		}

		/// <summary>
		/// Contains the raw data present in the target information block.
		/// </summary>
		public byte[] RawTargetInformation {
			get;
			private set;
		}

		/// <summary>
		/// Private constructor.
		/// </summary>
		private Type2Message() {
			TargetInformation = new Type2TargetInformation();
			OSVersion = new OSVersion();
		}

		/// <summary>
		/// Deserializes a Type 2 message instance from the specified buffer
		/// of bytes.
		/// </summary>
		/// <param name="buffer">The buffer containing a sequence of bytes
		/// representing an NTLM Type 2 message.</param>
		/// <returns>An initialized instance of the Type2 class.</returns>
		/// <exception cref="SerializationException">Thrown if an error occurs
		/// during deserialization of the Type 2 message.</exception>
		static internal Type2Message Deserialize(byte[] buffer) {
			try {
				Type2Message t2 = new Type2Message();
				using (var ms = new MemoryStream(buffer)) {
					using (var r = new BinaryReader(ms)) {
						if (r.ReadASCIIString(8) != signature)
							throw new InvalidDataException("Invalid signature.");
						if (r.ReadInt32() != (int) type)
							throw new InvalidDataException("Unexpected message type.");
						int targetLength = r.ReadInt16(), targetSpace =
							r.ReadInt16(), targetOffset = r.ReadInt32();
						t2.Flags = (Flags) r.ReadInt32();
						t2.Challenge = r.ReadBytes(8);
						// Figure out, which of the several versions of Type 2 we're
						// dealing with.
						t2.Version = GetType2Version(targetOffset);
						if (t2.Version > Type2Version.Version1) {
							t2.Context = r.ReadInt64();
							// Read the target information security buffer
							int informationLength = r.ReadInt16(), informationSpace =
								r.ReadInt16(), informationOffset = r.ReadInt32();
							t2.RawTargetInformation = new byte[informationLength];
							Array.Copy(buffer, informationOffset,
								t2.RawTargetInformation, 0, informationLength);
							// Version 3 has an additional OS version structure.
							if (t2.Version > Type2Version.Version2)
								t2.OSVersion = ReadOSVersion(r);
						}
						t2.TargetName = GetTargetName(r.ReadBytes(targetLength),
							(t2.Flags & Flags.NegotiateUnicode) == Flags.NegotiateUnicode);
						if (t2.Version > Type2Version.Version1) {
							t2.TargetInformation = ReadTargetInformation(r);
						}
					}
				}
				return t2;
			} catch (Exception e) {
				throw new SerializationException("NTLM Type 2 message could not be " +
					"deserialized.", e);
			}
		}

		/// <summary>
		/// Determines the version of an NTLM type 2 message.
		/// </summary>
		/// <param name="targetOffset">The target offset field of the NTLM
		/// type 2 message.</param>
		/// <returns>A value from the Type2Version enumeration.</returns>
		static Type2Version GetType2Version(int targetOffset) {
			var dict = new Dictionary<int, Type2Version>() {
				{ 32, Type2Version.Version1 },
				{ 48, Type2Version.Version2 },
				{ 56, Type2Version.Version3 }
			};
			return dict.ContainsKey(targetOffset) ? dict[targetOffset] :
				Type2Version.Unknown;
		}

		/// <summary>
		/// Reads the OS information data present in version 3 of an NTLM
		/// type 2 message from the specified BinaryReader.
		/// </summary>
		/// <param name="r">The BinaryReader instance to read from.</param>
		/// <returns>An initialized instance of the OSVersion class.</returns>
		static OSVersion ReadOSVersion(BinaryReader r) {
			OSVersion version = new OSVersion();
			version.MajorVersion = r.ReadByte();
			version.MinorVersion = r.ReadByte();
			version.BuildNumber = r.ReadInt16();
			// Swallow the reserved 32-bit word.
			r.ReadInt32();

			return version;
		}

		/// <summary>
		/// Reads the target information data present in version 2 and 3 of
		/// an NTLM type 2 message from the specified BinaryReader.
		/// </summary>
		/// <param name="r">The BinaryReader instance to read from.</param>
		/// <returns>An initialized instance of the Type2TargetInformation
		/// class.</returns>
		static Type2TargetInformation ReadTargetInformation(BinaryReader r) {
			Type2TargetInformation info = new Type2TargetInformation();
			while (true) {
				var _type = (Type2InformationType) r.ReadInt16();
				if (_type == Type2InformationType.TerminatorBlock)
					break;
				short length = r.ReadInt16();
				string content = Encoding.Unicode.GetString(r.ReadBytes(length));
				switch (_type) {
					case Type2InformationType.ServerName:
						info.ServerName = content;
						break;
					case Type2InformationType.DomainName:
						info.DomainName = content;
						break;
					case Type2InformationType.DnsHostname:
						info.DnsHostname = content;
						break;
					case Type2InformationType.DnsDomainName:
						info.DnsDomainName = content;
						break;
				}
			}
			return info;
		}

		/// <summary>
		/// Retrieves the target name from the specified byte array.
		/// </summary>
		/// <param name="data">A byte array containing the target name.</param>
		/// <param name="isUnicode">If true the target name will be decoded
		/// using UTF-16 unicode encoding.</param>
		/// <returns></returns>
		static string GetTargetName(byte[] data, bool isUnicode) {
			Encoding enc = isUnicode ? Encoding.Unicode : Encoding.ASCII;

			return enc.GetString(data);
		}
	}
}
