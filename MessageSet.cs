using System;
using System.Collections.Generic;
using System.Linq;

namespace S22.Imap
{
    /// <summary>
    /// A class to assist in the creation of IMAP-compatible message sets.
    /// </summary>
    public class MessageSet
    {
        private HashSet<uint> _ids = new HashSet<uint>();

        /// <summary>
        /// Initializes a new instance of the MessageSet class.
        /// </summary>
        public MessageSet() { }

        /// <summary>
        /// Initializes a new instance of the MessageSet class,
        /// and adds the given id to the set.
        /// </summary>
        /// <param name="id"></param>
        public MessageSet(uint id)
        {
            Add(id);
        }

        /// <summary>
        /// Initializes a new instance of the MessageSet class,
        /// and adds the given list of ids to the set.
        /// </summary>
        /// <param name="ids"></param>
        public MessageSet(IEnumerable<uint> ids)
        {
            Add(ids);
        }

        /// <summary>
        /// Initializes a new instance of the MessageSet class,
        /// and adds the given range of ids to the set.
        /// </summary>
        /// <param name="fromId"></param>
        /// <param name="toId"></param>
        public MessageSet(uint fromId, uint toId)
        {
            AddRange(fromId, toId);
        }

        /// <summary>
        /// Returns the number of unique message IDs in the set.
        /// </summary>
        /// <returns></returns>
        public int Count
        {
            get
            {
                return _ids.Count;
            }
        }

        /// <summary>
        /// Adds a message id to the set, if it does not already exist.
        /// </summary>
        /// <param name="id"></param>
        public void Add(uint id)
        {
            _ids.Add(id);
        }

        /// <summary>
        /// Adds a list of ids to the set, where they do not already exist.
        /// </summary>
        /// <param name="ids"></param>
        public void Add(IEnumerable<uint> ids)
        {
            foreach (uint id in ids)
                Add(id);
        }

        /// <summary>
        /// Adds a range of ids to the set, where they do not already exist.
        /// </summary>
        /// <param name="fromId"></param>
        /// <param name="toId"></param>
        public void AddRange(uint fromId, uint toId)
        {
            if (fromId > toId)
                throw new ArgumentException("The second range value must be greater than or equal to the first.");
            for (uint i = fromId; i <= toId; i++)
                _ids.Add(i);
        }

        /// <summary>
        /// Removes a message id from the set.
        /// </summary>
        /// <param name="id"></param>
        public void Remove(uint id)
        {
            _ids.Remove(id);
        }

        /// <summary>
        /// Removes a list of ids from the set.
        /// </summary>
        /// <param name="ids"></param>
        public void Remove(IEnumerable<uint> ids)
        {
            foreach (uint id in ids)
                Remove(id);
        }

        /// <summary>
        /// Removes a range of ids from the set.
        /// </summary>
        /// <param name="fromId"></param>
        /// <param name="toId"></param>
        public void RemoveRange(uint fromId, uint toId)
        {
            if (fromId > toId)
                throw new ArgumentException("The second range value must be greater than or equal to the first.");
            for (uint i = fromId; i <= toId; i++)
                _ids.Remove(i);
        }

        /// <summary>
        /// Returns a message set string ready for use in IMAP commands.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Join(",", getImapMessageSetRanges(_ids));
        }

        /// <summary>
        /// Converts a list of integers into a string array of message sets
        /// suitable for use with IMAP commands.
        /// E.g., input {1,3,4,5,7} returns {"1", "3:5", "7"}
        /// </summary>
        /// <param name="ints"></param>
        /// <returns></returns>
        private string[] getImapMessageSetRanges(HashSet<uint> ints)
        {
            if (ints.Count < 1)
                return new string[]{};
            List<uint> list = ints.ToList<uint>();
            if (list.Count == 1)
                return new string[] { list[0].ToString() };
            list.Sort();
            var lng = ints.Count;
            var fromNums = new List<uint>();
            var toNums = new List<uint>();
            for (var i = 0; i < lng - 1; i++)
            {
                if(i == 0)
                    fromNums.Add(list[i]);
                if (list[i + 1] > list[i] + 1)
                {
                    toNums.Add(list[i]);
                    fromNums.Add(list[i + 1]);
                }
            }
            toNums.Add(list[lng - 1]);
            return Enumerable.Range(0, toNums.Count).Select(
                i => fromNums[i].ToString() +
                    (toNums[i] == fromNums[i] ? "" : ":" + toNums[i].ToString())
            ).ToArray();
            
        }
    }
}
