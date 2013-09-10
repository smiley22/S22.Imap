using System;
using System.Collections.Generic;
using System.Linq;

namespace S22.Imap
{
    public class MessageSet
    {
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

        private SortedSet<uint> _ids = new SortedSet<uint>();

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
            //Just add them to the list for now. It makes it easier to convert to ranges later
            //if we are just starting from an int list.
            for (uint i = fromId; i <= toId; i++)
                _ids.Add(i);
        }

        /// <summary>
        /// Returns a message set string ready for use in IMAP commands.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            IList<Tuple<uint, uint>> ranges = convertIDsToRanges();
            IEnumerable<uint> ids = removeIDsInRanges(ranges);
            IList<string> values = ids.Select(i => i.ToString()).ToList();
            foreach (Tuple<uint, uint> t in ranges)
                values.Add(string.Concat(t.Item1 + ":" + t.Item2));
            return string.Join(",", values);
        }

        /// <summary>
        /// Converts contiguous ids to ranges and removes them from the id list.
        /// </summary>
        private IList<Tuple<uint, uint>> convertIDsToRanges()
        {
            IList<Tuple<uint, uint>> ranges = new List<Tuple<uint, uint>>();

            int? startIndex = null;
            bool inRange = false;
            for (int i = 0; i < _ids.Count; i++)
            {
                if (startIndex == null)
                {
                    startIndex = i;
                    continue;
                }
                if ((_ids.ElementAt(i) - _ids.ElementAt(startIndex.Value) == i - startIndex) && i != _ids.Count - 1)
                {
                    inRange = true;
                    continue; //contiguous
                }
                else if (_ids.ElementAt(i) - _ids.ElementAt(startIndex.Value) != i - startIndex)
                {
                    ranges.Add(new Tuple<uint, uint>(_ids.ElementAt(startIndex.Value), _ids.ElementAt(i - 1)));
                    inRange = false;
                    startIndex = i;
                }
                else if (i == _ids.Count - 1 && inRange)
                {
                    ranges.Add(new Tuple<uint, uint>(_ids.ElementAt(startIndex.Value), _ids.ElementAt(i)));
                }

            }


            return ranges;
            
        }

        /// <summary>
        /// Removes any ids from the list that are already covered by ranges
        /// </summary>
        private IEnumerable<uint> removeIDsInRanges(IList<Tuple<uint, uint>> ranges)
        {
            IList<uint> ids = new List<uint>();
            for (int i = _ids.Count - 1; i > -1; i--)
                if (!rangeContains(ranges, _ids.ElementAt(i)))
                    ids.Add(_ids.ElementAt(i));
            return ids;
        }

        /// <summary>
        /// Returns true if a range already covers a given id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool rangeContains(IList<Tuple<uint, uint>> ranges, uint id)
        {
            foreach (var r in ranges)
                if (id >= r.Item1 && id <= r.Item2)
                    return true;
            return false;
        }
    }
}
