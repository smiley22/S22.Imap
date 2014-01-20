using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using S22.Imap;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for constructing IMAP sequence-sets. 
	/// </summary>
	[TestClass]
	public class SequenceSetTest {
		/// <summary>
		/// Ensures overlapping ranges are collapsed into a sequence.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void CollapseOverlappingRanges() {
			var list = new List<uint>() { 1, 2, 3, 2, 3, 4 };
			Assert.AreEqual("1:4", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Ensures contiguous ranges are collapsed into a sequence.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void CollapseContiguousRanges() {
			var list = new List<uint>() { 1, 2, 3, 4, 5, 6 };
			Assert.AreEqual("1:6", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Ensures duplicates are properly removed.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void RemoveDuplicateUIDs() {
			var list = new List<uint>() { 1, 1 };
			Assert.AreEqual("1", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Ensures duplicates are properly removed.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void UIDsDoNotOverlapRanges() {
			var list = new List<uint>() { 1, 2, 3, 2 };
			Assert.AreEqual("1:3", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Ensures duplicates are properly removed.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void RangesDoNotOverlapUIDs() {
			var list = new List<uint>() { 2, 1, 2, 3 };
			Assert.AreEqual("1:3", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Ensures ranges and single UIDs are properly converted.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void RangesAndUIDs() {
			var list = new List<uint>() { 1, 3, 4, 5, 7 };
			Assert.AreEqual("1,3:5,7", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Ensures a single UID is properly converted.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		public void SingleUID() {
			var list = new List<uint>() { 4 };
			Assert.AreEqual("4", Util.BuildSequenceSet(list));
		}

		/// <summary>
		/// Passing null to Util.BuildSequenceSet should raise an ArgumentNullException.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		[ExpectedException(typeof(ArgumentNullException))]
		public void ThrowOnNullArgument() {
			Util.BuildSequenceSet(null);
		}

		/// <summary>
		/// Passing an empty collection to Util.BuildSequenceSet should raise an ArgumentException.
		/// </summary>
		[TestMethod]
		[TestCategory("BuildSequenceSet")]
		[ExpectedException(typeof(ArgumentException))]
		public void ThrowOnEmptyCollection() {
			Util.BuildSequenceSet(new HashSet<uint>());
		}
	}
}
