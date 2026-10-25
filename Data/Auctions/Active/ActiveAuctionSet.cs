using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Coflnet.Sky.Core
{
    public enum RemoveReason
    {
        Sold,
        Ended,
        Expired,
        Cancelled,
        Summary,
        Manual
    }

    /// <summary>
    /// Thread safe in-memory set of currently active auctions keyed by <see cref="SaveAuction.UId"/>.
    /// Has no database or kafka dependencies, loading and feeding is done by the owner.
    /// </summary>
    public class ActiveAuctionSet
    {
        private static readonly TimeSpan TombstoneAge = TimeSpan.FromMinutes(10);

        private readonly ConcurrentDictionary<long, SaveAuction> auctions = new();
        private readonly ConcurrentDictionary<long, DateTime> tombstones = new();
        private readonly object rebuildLock = new();
        private long version;
        private long lastChangeTicks = DateTime.UtcNow.Ticks;
        private ActiveAuctionSnapshot snapshot = ActiveAuctionSnapshot.Empty;
        private volatile bool ready;

        public event Action<SaveAuction, string> OnUpserted;
        public event Action<long, RemoveReason> OnRemoved;

        public int Count => auctions.Count;
        public bool IsReady { get => ready; private set => ready = value; }
        public DateTime LastChange => new(Interlocked.Read(ref lastChangeTicks), DateTimeKind.Utc);

        public void MarkReady() => IsReady = true;

        public ActiveAuctionSnapshot Snapshot => Volatile.Read(ref snapshot);

        public ActiveAuctionSnapshot GetFreshSnapshot() => RebuildSnapshotIfDirty();

        private void Bump()
        {
            Interlocked.Increment(ref version);
            Interlocked.Exchange(ref lastChangeTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// Adds or replaces an auction. Returns false when it was rejected (invalid, ended, tombstoned or stale).
        /// The passed object is normalized in place and then owned by the set.
        /// </summary>
        public bool Upsert(SaveAuction a, string source)
        {
            if (a == null || a.UId == 0)
                return false;
            var now = DateTime.UtcNow;
            if (a.End <= now)
                return false;
            if (tombstones.TryGetValue(a.UId, out var removedAt) && now - removedAt < TombstoneAge)
                return false;
            if (auctions.TryGetValue(a.UId, out var existing) && IsFresher(existing, a))
                return false;

            Normalize(a);

            var accepted = true;
            auctions.AddOrUpdate(a.UId, a, (_, stored) =>
            {
                if (IsFresher(stored, a))
                {
                    accepted = false;
                    return stored;
                }
                return a;
            });
            if (!accepted)
                return false;
            Bump();
            try { OnUpserted?.Invoke(a, source); } catch { /* handlers must not break the feed */ }
            return true;
        }

        private static bool IsFresher(SaveAuction stored, SaveAuction incoming)
        {
            return (stored.Bids?.Count ?? 0) > (incoming.Bids?.Count ?? 0)
                || stored.HighestBidAmount > incoming.HighestBidAmount;
        }

        private static void Normalize(SaveAuction a)
        {
            a.Tag = StringPool.Intern(a.Tag);
            a.ItemName = StringPool.Intern(a.ItemName);
            a.AuctioneerId = StringPool.Intern(a.AuctioneerId);
            a.ProfileId = StringPool.Intern(a.ProfileId);
            try
            {
                var flat = a.FlatenedNBT;
                if (flat != null)
                {
                    var interned = new Dictionary<string, string>(flat.Count);
                    foreach (var pair in flat)
                        interned[StringPool.Intern(pair.Key)] = pair.Value;
                    a.FlatenedNBT = interned;
                }
            }
            catch
            {
                // broken nbt, keep the auction without flat data
            }
            a.Context = null;
            a.CoopMembers = null;
            a.ClaimedBids = null;
            a.NBTLookup = null;
            a.Enchantments ??= new();
            a.Bids ??= new();
        }

        public bool Remove(long uid, RemoveReason reason)
        {
            var removed = auctions.TryRemove(uid, out _);
            tombstones[uid] = DateTime.UtcNow;
            if (!removed)
                return false;
            Bump();
            try { OnRemoved?.Invoke(uid, reason); } catch { }
            return true;
        }

        public bool TryGet(long uid, out SaveAuction a) => auctions.TryGetValue(uid, out a);

        public int ExpireEnded(DateTime now)
        {
            var count = 0;
            foreach (var pair in auctions)
            {
                if (pair.Value.End <= now && auctions.TryRemove(pair.Key, out _))
                {
                    count++;
                    Bump();
                    try { OnRemoved?.Invoke(pair.Key, RemoveReason.Expired); } catch { }
                }
            }
            return count;
        }

        public int PurgeTombstones(DateTime now)
        {
            var count = 0;
            foreach (var pair in tombstones)
            {
                if (now - pair.Value > TombstoneAge && tombstones.TryRemove(pair.Key, out _))
                    count++;
            }
            return count;
        }

        public Dictionary<long, long> UidEndTicks()
        {
            var result = new Dictionary<long, long>(auctions.Count);
            foreach (var pair in auctions)
                result[pair.Key] = pair.Value.End.Ticks;
            return result;
        }

        public IEnumerable<SaveAuction> BySeller(string auctioneerId)
        {
            if (auctioneerId == null)
                return Enumerable.Empty<SaveAuction>();
            var snap = Snapshot;
            if (!snap.BySeller.TryGetValue(auctioneerId, out var indices))
                return Enumerable.Empty<SaveAuction>();
            return indices.Select(i => snap.All[i]);
        }

        /// <summary>Rebuilds the snapshot only if something changed since the last build</summary>
        public ActiveAuctionSnapshot RebuildSnapshotIfDirty()
        {
            var current = Volatile.Read(ref snapshot);
            if (current.Version == Interlocked.Read(ref version))
                return current;
            lock (rebuildLock)
            {
                current = Volatile.Read(ref snapshot);
                var v = Interlocked.Read(ref version);
                if (current.Version == v)
                    return current;
                var pairs = auctions.ToArray();
                var uids = new long[pairs.Length];
                var all = new SaveAuction[pairs.Length];
                for (int p = 0; p < pairs.Length; p++)
                {
                    uids[p] = pairs[p].Key;
                    all[p] = pairs[p].Value;
                }
                Array.Sort(uids, all); // ordered by UId, much cheaper than OrderBy with a key selector
                var byTag = new Dictionary<string, List<int>>();
                var bySeller = new Dictionary<string, List<int>>();
                var byEnchant = new List<int>[256]; // indexed by the stored enchantment type byte
                var byTier = new Dictionary<Tier, List<int>>();
                var byReforge = new Dictionary<ItemReferences.Reforge, List<int>>();
                var bin = new List<int>();
                var color = new List<int>();
                var byColorValue = new Dictionary<long, List<int>>();
                for (int i = 0; i < all.Length; i++)
                {
                    var a = all[i];
                    Add(byTag, a.Tag, i);
                    Add(bySeller, a.AuctioneerId, i);
                    Add(byTier, a.Tier, i);
                    Add(byReforge, a.Reforge, i);
                    if (a.Bin)
                        bin.Add(i);
                    if (a.Enchantments != null)
                        foreach (var e in a.Enchantments)
                        {
                            var list = byEnchant[(byte)e.Type] ??= new List<int>();
                            if (list.Count == 0 || list[^1] != i) // same type twice on one auction
                                list.Add(i);
                        }
                    try
                    {
                        if (a.FlatenedNBT != null && a.FlatenedNBT.TryGetValue("color", out var colorString))
                        {
                            color.Add(i);
                            try
                            {
                                var colorValue = NBT.GetColor(colorString);
                                if (!byColorValue.TryGetValue(colorValue, out var colorList))
                                    byColorValue[colorValue] = colorList = new List<int>();
                                colorList.Add(i);
                            }
                            catch { /* unparsable color, only reachable via WithColor */ }
                        }
                    }
                    catch { /* broken nbt */ }
                }
                var built = new ActiveAuctionSnapshot(all, ToArrays(byTag), ToArrays(bySeller), v, DateTime.UtcNow,
                    EnchantArrays(byEnchant), ToArrays(byTier), ToArrays(byReforge), bin.ToArray(), color.ToArray(),
                    byColorValue.ToDictionary(c => c.Key, c => c.Value.ToArray()));
                Volatile.Write(ref snapshot, built);
                return built;
            }
        }

        private static void Add(Dictionary<string, List<int>> index, string key, int i)
        {
            if (key == null)
                return;
            if (!index.TryGetValue(key, out var list))
                index[key] = list = new List<int>();
            list.Add(i);
        }

        private static void Add<TKey>(Dictionary<TKey, List<int>> index, TKey key, int i)
            => GetList(index, key).Add(i);

        private static List<int> GetList<TKey>(Dictionary<TKey, List<int>> index, TKey key)
        {
            if (!index.TryGetValue(key, out var list))
                index[key] = list = new List<int>();
            return list;
        }

        private static Dictionary<byte, int[]> EnchantArrays(List<int>[] index)
        {
            var result = new Dictionary<byte, int[]>();
            for (int t = 0; t < index.Length; t++)
                if (index[t] != null)
                    result[(byte)t] = index[t].ToArray();
            return result;
        }

        private static Dictionary<TKey, int[]> ToArrays<TKey>(Dictionary<TKey, List<int>> index)
            => index.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }
}
