using System.Collections.Generic;
using System.Threading;

namespace S22.Imap {
	/// <summary>
	/// A thread-safe Queue.
	/// </summary>
	internal class SafeQueue<T> {
		readonly Queue<T> _queue = new Queue<T>();

		/// <summary>
		/// Adds an object to the end of the queue.
		/// </summary>
		/// <param name="item">The object to add to the queue.</param>
		public void Enqueue(T item) {
			lock (_queue) {
				_queue.Enqueue(item);
				if (_queue.Count == 1)
					Monitor.PulseAll(_queue);
			}
		}

		/// <summary>
		/// Removes and returns the object at the beginning of the queue. If the queue is empty, the
		/// method blocks the calling thread until an object is put into the queue by another thread.
		/// </summary>
		/// <returns>The object that was removed from the beginning of the queue.</returns>
		public T Dequeue() {
			lock (_queue) {
				while (_queue.Count == 0)
					Monitor.Wait(_queue);
				return _queue.Dequeue();
			}
		}
	}
}
