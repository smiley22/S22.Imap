using S22.Imap.Auth;
using System;
using System.Linq;
using System.Numerics;

namespace S22.Imap.Auth.Sasl.Mechanisms.Srp {
	/// <summary>
	/// Represents a "multi-precision integer" (MPI) as is described in the
	/// SRP specification (3.2 Multi-Precision Integers, p.5).
	/// </summary>
	/// <remarks>Multi-Precision Integers, or MPIs, are positive integers used
	/// to hold large integers used in cryptographic computations.</remarks>
	internal class Mpi {
		/// <summary>
		/// The underlying BigInteger instance used to represent this
		/// "multi-precision integer".
		/// </summary>
		public BigInteger Value {
			get;
			set;
		}

		/// <summary>
		/// Creates a new "multi-precision integer" from the specified array
		/// of bytes.
		/// </summary>
		/// <param name="data">A big-endian sequence of bytes forming the
		/// integer value of the multi-precision integer.</param>
		public Mpi(byte[] data) {
			byte[] b = new byte[data.Length];
			Array.Copy(data.Reverse().ToArray(), b, data.Length);
			ByteBuilder builder = new ByteBuilder().Append(b);
			// We append a null byte to the buffer which ensures the most
			// significant bit will never be set and the big integer value
			// always be positive.
			if (b.Last() != 0)
				builder.Append(0);
			Value = new BigInteger(builder.ToArray());

		}

		/// <summary>
		/// Creates a new "multi-precision integer" from the specified BigInteger
		/// instance.
		/// </summary>
		/// <param name="value">The BigInteger instance to initialize the MPI
		/// with.</param>
		public Mpi(BigInteger value)
			: this(value.ToByteArray().Reverse().ToArray()) {
		}

		/// <summary>
		/// Returns a sequence of bytes in big-endian order forming the integer
		/// value of this "multi-precision integer" instance.
		/// </summary>
		/// <returns>Returns a sequence of bytes in big-endian order representing
		/// this "multi-precision integer" instance.</returns>
		public byte[] ToBytes() {
			byte[] b = Value.ToByteArray().Reverse().ToArray();
			// Strip off the 0 byte.
			if (b[0] == 0)
				return b.Skip(1).ToArray();
			return b;
		}

		/// <summary>
		/// Serializes the "multi-precision integer" into a sequence of bytes
		/// according to the requirements of the SRP specification.
		/// </summary>
		/// <returns>A big-endian sequence of bytes representing the integer
		/// value of the MPI.</returns>
		public byte[] Serialize() {
			// MPI's expect a big-endian sequence of bytes forming the integer
			// value, whereas BigInteger uses little-endian.
			byte[] data = ToBytes();
			ushort length = Convert.ToUInt16(data.Length);

			return new ByteBuilder()
				.Append(length, true)
				.Append(data)
				.ToArray();
		}
	}
}
