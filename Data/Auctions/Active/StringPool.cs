using System.Collections.Concurrent;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Process wide string interning for the repeated values of active auctions (tags, names, uuids).
    /// Unlike <see cref="string.Intern"/> the pool can be reset so it never grows without bounds.
    /// </summary>
    public static class StringPool
    {
        private const int MaxEntries = 100_000;
        private static readonly ConcurrentDictionary<string, string> pool = new();

        public static string Intern(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            if (pool.Count > MaxEntries)
                pool.Clear();
            return pool.GetOrAdd(s, s);
        }
    }
}
