using System;
using System.Collections.Generic;

namespace CSBMSParser
{
    internal static class MethodExtensions
    {
        public static IEnumerable<KeyValuePair<T, D>> descendingMap<T, D>(this IDictionary<T, D> self)
        {
            if (self is SortedList<T, D> sortedList)
            {
                for (int i = sortedList.Count - 1; i >= 0; i--)
                {
                    yield return new KeyValuePair<T, D>(sortedList.Keys[i], sortedList.Values[i]);
                }
                yield break;
            }

            var entries = new List<KeyValuePair<T, D>>(self.Count);
            foreach (var entry in self)
            {
                entries.Add(entry);
            }

            var comparer = Comparer<T>.Default;
            entries.Sort((left, right) => comparer.Compare(right.Key, left.Key));
            foreach (var entry in entries)
            {
                yield return entry;
            }
        }

        public static KeyValuePair<T, D> lowerEntry<T, D>(this IDictionary<T, D> self, T key)
        {
            if (self is SortedList<T, D> sortedList)
            {
                int index = FindLowerIndex(sortedList.Keys, key);
                return index >= 0
                    ? new KeyValuePair<T, D>(sortedList.Keys[index], sortedList.Values[index])
                    : default;
            }

            var comparer = Comparer<T>.Default;
            var found = false;
            var best = default(KeyValuePair<T, D>);
            foreach (var entry in self)
            {
                if (comparer.Compare(entry.Key, key) >= 0)
                {
                    continue;
                }

                if (!found || comparer.Compare(entry.Key, best.Key) > 0)
                {
                    found = true;
                    best = entry;
                }
            }

            return found ? best : default;
        }

        public static IDictionary<T, D> subMap<T, D>(this IDictionary<T, D> self, T fromKey, bool fromInclusive, T toKey, bool toInclusive)
        {
            var comparer = Comparer<T>.Default;
            var map = new Dictionary<T, D>();

            if (self is SortedList<T, D> sortedList)
            {
                for (int i = 0; i < sortedList.Count; i++)
                {
                    var key = sortedList.Keys[i];
                    if (IsInSubMap(key, fromKey, fromInclusive, toKey, toInclusive, comparer))
                    {
                        map[key] = sortedList.Values[i];
                    }
                }
                return map;
            }

            foreach (var entry in self)
            {
                if (IsInSubMap(entry.Key, fromKey, fromInclusive, toKey, toInclusive, comparer))
                {
                    map[entry.Key] = entry.Value;
                }
            }

            return map;
        }

        private static bool IsInSubMap<T>(T key, T fromKey, bool fromInclusive, T toKey, bool toInclusive, Comparer<T> comparer)
        {
            int comparison = comparer.Compare(fromKey, key);
            if (comparison < 0 || (comparison == 0 && !fromInclusive))
            {
                return false;
            }

            comparison = comparer.Compare(key, toKey);
            if (comparison < 0 || (comparison == 0 && !toInclusive))
            {
                return false;
            }

            return true;
        }

        private static int FindLowerIndex<T>(IList<T> keys, T key)
        {
            var comparer = Comparer<T>.Default;
            int left = 0;
            int right = keys.Count - 1;
            int result = -1;

            while (left <= right)
            {
                int middle = left + ((right - left) / 2);
                if (comparer.Compare(keys[middle], key) < 0)
                {
                    result = middle;
                    left = middle + 1;
                }
                else
                {
                    right = middle - 1;
                }
            }

            return result;
        }

        public static string substring(this string s, int start) => s.substring(start, s.Length);
        public static string substring(this string s, int start, int end) => s.Substring(start, end - start);
    }
}
