using System.IO;
using System.Text;

namespace S22.Imap.Test {
	/// <summary>
	/// A memory stream for mocking the ImapClient class.
	/// </summary>
	internal class MockStream : MemoryStream {
		/// <summary>
		/// Creates and initializes a new instance of the MockStream class using
		/// the specified mock text.
		/// </summary>
		/// <param name="mockFile">A string to initialize the underlying
		/// MemoryStream with.</param>
		public MockStream(string mockText): base(Encoding.ASCII.GetBytes(mockText)) {
		}

		/// <summary>
		/// A dummy method which does nothing.
		/// </summary>
		/// <param name="buffer">The buffer to write data from.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which
		/// to begin copying bytes to the current stream.</param>
		/// <param name="count">The maximum number of bytes to write.</param>
		public override void Write(byte[] buffer, int offset, int count) {
		}

		/// <summary>
		/// A dummy method which does nothing.
		/// </summary>
		/// <param name="value">The byte to write.</param>
		public override void WriteByte(byte value) {
		}
	}
}
