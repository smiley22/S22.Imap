using System;

namespace S22.Imap.Auth.Sasl.Mechanisms.Ntlm {
	/// <summary>
	/// Represents an NTLM security buffer, which is a structure used to point
	/// to a buffer of binary data within an NTLM message.
	/// </summary>
	internal class SecurityBuffer {
		/// <summary>
		/// The length of the buffer content in bytes (may be zero).
		/// </summary>
		public short Length {
			get;
			private set;
		}

		/// <summary>
		/// The allocated space for the buffer in bytes (typically the same as
		/// the length).
		/// </summary>
		public short AllocatedSpace {
			get {
				return Length;
			}
		}

		/// <summary>
		/// The offset from the beginning of the NTLM message to the start of
		/// the buffer, in bytes.
		/// </summary>
		public int Offset {
			get;
			private set;
		}

		/// <summary>
		/// Creates a new instance of the SecurityBuffer class using the specified
		/// values.
		/// </summary>
		/// <param name="length">The length of the buffer described by this instance
		/// of the SecurityBuffer class.</param>
		/// <param name="offset">The offset at which the buffer starts, in bytes.</param>
		/// <exception cref="OverflowException">Thrown if the length value exceeds
		/// the maximum value allowed. The security buffer structure stores the
		/// length value as a 2-byte short value.</exception>
		public SecurityBuffer(int length, int offset) {
			Length = Convert.ToInt16(length);
			Offset = offset;
		}

		/// <summary>
		/// Creates a new instance of the SecurityBuffer class using the specified
		/// values.
		/// </summary>
		/// <param name="data">The data of the buffer described by this instance
		/// of the SecurityBuffer class.</param>
		/// <param name="offset">The offset at which the buffer starts, in bytes.</param>
		/// <exception cref="OverflowException">Thrown if the length of the data
		/// buffer exceeds the maximum value allowed. The security buffer structure
		/// stores the buffer length value as a 2-byte short value.</exception>
		public SecurityBuffer(byte[] data, int offset)
			: this(data.Length, offset) {
		}

		/// <summary>
		/// Serializes this instance of the SecurityBuffer into an array of bytes.
		/// </summary>
		/// <returns>A byte array representing this instance of the SecurityBuffer
		/// class.</returns>
		public byte[] Serialize() {
			return new ByteBuilder()
				.Append(Length)
				.Append(AllocatedSpace)
				.Append(Offset)
				.ToArray();
		}
	}
}
