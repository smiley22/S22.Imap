using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace S22.Imap.Test
{
    /// <summary>
    /// Contains unit tests for the MessageSet class
    /// </summary>
    [TestClass]
    public class MessageSetTest
    {
        [TestMethod]
        [TestCategory("MessageSet")]
        public void CollapseOverlappingRanges()
        {
            var set = new MessageSet();
            set.AddRange(1, 3);
            set.AddRange(2, 4);
            Assert.AreEqual("1:4", set.ToString());
        }

        [TestMethod]
        [TestCategory("MessageSet")]
        public void CollapseContiguousRanges()
        {
            var set = new MessageSet();
            set.AddRange(1, 3);
            set.AddRange(4, 6);
            Assert.AreEqual("1:6", set.ToString());
        }

        [TestMethod]
        [TestCategory("MessageSet")]
        public void RemoveDuplicateIDs()
        {
            var set = new MessageSet();
            set.Add(1);
            set.Add(1);
            Assert.AreEqual("1", set.ToString());
        }

        [TestMethod]
        [TestCategory("MessageSet")]
        public void IDsDoNotOverlapRanges()
        {
            var set = new MessageSet();
            set.AddRange(1, 3);
            set.Add(2);
            Assert.AreEqual("1:3", set.ToString());
        }

        [TestMethod]
        [TestCategory("MessageSet")]
        public void RangesDoNotOverlapIDs()
        {
            var set = new MessageSet();
            set.Add(2);
            set.AddRange(1, 3);
            Assert.AreEqual("1:3", set.ToString());
        }


        [TestMethod]
        [TestCategory("MessageSet")]
        public void RangesAndIDs()
        {
            var set = new MessageSet();
            set.Add(1);
            set.AddRange(3, 5);
            set.Add(7);
            Assert.AreEqual("1,3:5,7", set.ToString());
        }
    }
}
