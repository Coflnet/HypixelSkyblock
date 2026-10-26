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
        private readonly object writeLock = new();
        /// <summary>All stored uids in ascending order, maintained incrementally so a rebuild needs no sort</summary>
        private readonly List<long> sortedUids = new();
        private long version;
        private long rebuildCount;
        private long lastRebuildTicks;
        private volatile bool forceNextRebuild = true;
        /// <summary>true while <see cref="sortedUids"/> got appended out of order during the initial load</summary>
        private bool unsorted;
        private long lastChangeTicks = DateTime.UtcNow.Ticks;
        private ActiveAuctionSnapshot snapshot = ActiveAuctionSnapshot.Empty;
        private volatile bool ready;

        public event Action<SaveAuction, string> OnUpserted;
        public event Action<long, RemoveReason> OnRemoved;
        /// <summary>Raised after a snapshot rebuild with the time it took</summary>
        public event Action<TimeSpan> OnSnapshotRebuilt;

        /// <summary>Default for <see cref="SnapshotMinInterval"/></summary>
        public const int SnapshotMinIntervalSeconds = 10;

        /// <summary>A dirty set is rebuilt at most this often (except the first build and the first one after <see cref="MarkReady"/>)</summary>
        public TimeSpan SnapshotMinInterval { get; set; } = TimeSpan.FromSeconds(SnapshotMinIntervalSeconds);
        /// <summary>Number of snapshot rebuilds so far</summary>
        public long RebuildCount => Interlocked.Read(ref rebuildCount);
        public TimeSpan LastRebuildDuration => TimeSpan.FromTicks(Interlocked.Read(ref lastRebuildTicks));
        /// <summary>Current mutation counter, only moves when the content really changed</summary>
        public long Version => Interlocked.Read(ref version);

        public int Count => auctions.Count;
        public bool IsReady { get => ready; private set => ready = value; }
        public DateTime LastChange => new(Interlocked.Read(ref lastChangeTicks), DateTimeKind.Utc);

        public void MarkReady()
        {
            forceNextRebuild = true; // the first build after loading must not wait for the interval
            lock (writeLock)
                EnsureSorted();
            IsReady = true;
        }

        public ActiveAuctionSnapshot Snapshot => Volatile.Read(ref snapshot);

        /// <summary>
        /// Rebuilds a dirty snapshot right away, ignoring <see cref="SnapshotMinInterval"/>.
        /// For consumers that need a consistent full state (the full stream endpoint).
        /// </summary>
        public ActiveAuctionSnapshot GetFreshSnapshot() => Rebuild(true);

        private void Bump()
        {
            Interlocked.Increment(ref version);
            Interlocked.Exchange(ref lastChangeTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// Adds or replaces an auction. Returns false when it was rejected (invalid, ended, tombstoned, stale or unchanged).
        /// The passed object is normalized in place and then owned by the set. The version is only bumped when the stored copy changes.
        /// </summary>
        public bool Upsert(SaveAuction a, string source)
        {
            if (!WouldAccept(a))
                return false;

            Normalize(a);

            lock (writeLock)
            {
                // state may have changed since the check above
                if (!WouldAccept(a))
                    return false;
                if (auctions.TryAdd(a.UId, a))
                    InsertSorted(a.UId);
                else
                    auctions[a.UId] = a;
                Bump();
            }
            try { OnUpserted?.Invoke(a, source); } catch { /* handlers must not break the feed */ }
            return true;
        }

        /// <summary>
        /// Whether <see cref="Upsert"/> would store the candidate. Same rules, does not mutate anything.
        /// </summary>
        public bool WouldAccept(SaveAuction candidate)
        {
            if (candidate == null || candidate.UId == 0)
                return false;
            var now = DateTime.UtcNow;
            if (candidate.End <= now)
                return false;
            if (tombstones.TryGetValue(candidate.UId, out var removedAt) && now - removedAt < TombstoneAge)
                return false;
            if (!auctions.TryGetValue(candidate.UId, out var existing))
                return true;
            return !IsFresher(existing, candidate) && Differs(existing, candidate);
        }

        private static bool IsFresher(SaveAuction stored, SaveAuction incoming)
        {
            return (stored.Bids?.Count ?? 0) > (incoming.Bids?.Count ?? 0)
                || stored.HighestBidAmount > incoming.HighestBidAmount;
        }

        private static bool Differs(SaveAuction stored, SaveAuction incoming)
        {
            if (stored.End != incoming.End
                || (stored.Bids?.Count ?? 0) != (incoming.Bids?.Count ?? 0)
                || stored.HighestBidAmount != incoming.HighestBidAmount
                || (incoming.ItemId != 0 && stored.ItemId != incoming.ItemId)
                || (stored.Tag ?? string.Empty) != (incoming.Tag ?? string.Empty)
                || (stored.ItemName ?? string.Empty) != (incoming.ItemName ?? string.Empty)
                || IdentityChanged(stored.AuctioneerId, incoming.AuctioneerId)
                || IdentityChanged(stored.ProfileId, incoming.ProfileId)
                || BidderIdentityChanged(stored.Bids, incoming.Bids))
                return true;
            try
            {
                return (stored.FlatenedNBT?.Count ?? 0) == 0 && (incoming.FlatenedNBT?.Count ?? 0) > 0;
            }
            catch
            {
                return false; // broken nbt on either side, nothing to gain
            }
        }

        /// <summary>
        /// Opt-out masking replaces ids with a random anonymous uuid each time, so two anonymous values are equal
        /// but anonymous vs real (or two different real values) is a change that must reach the set.
        /// </summary>
        private static bool IdentityChanged(string stored, string incoming)
        {
            var storedAnon = PlayerOptOut.IsAnonymousUuid(stored);
            var incomingAnon = PlayerOptOut.IsAnonymousUuid(incoming);
            if (storedAnon && incomingAnon)
                return false;
            if (storedAnon != incomingAnon)
                return true;
            return stored != incoming;
        }

        private static bool BidderIdentityChanged(List<SaveBids> stored, List<SaveBids> incoming)
        {
            if (stored == null || incoming == null || stored.Count == 0 || stored.Count != incoming.Count)
                return false; // count changes are handled by the caller
            var storedAnon = 0;
            var incomingAnon = 0;
            List<string> storedReal = null;
            List<string> incomingReal = null;
            foreach (var bid in stored)
            {
                if (PlayerOptOut.IsAnonymousUuid(bid.Bidder))
                    storedAnon++;
                else
                    (storedReal ??= new()).Add(bid.Bidder);
            }
            foreach (var bid in incoming)
            {
                if (PlayerOptOut.IsAnonymousUuid(bid.Bidder))
                    incomingAnon++;
                else
                    (incomingReal ??= new()).Add(bid.Bidder);
            }
            if (storedAnon != incomingAnon)
                return true;
            if (storedReal == null && incomingReal == null)
                return false;
            if (storedReal == null || incomingReal == null)
                return true;
            storedReal.Sort(StringComparer.Ordinal); // bid order may differ between sources
            incomingReal.Sort(StringComparer.Ordinal);
            return !storedReal.SequenceEqual(incomingReal);
        }

        private void EnsureSorted()
        {
            if (!unsorted)
                return;
            sortedUids.Sort();
            unsorted = false;
        }

        private void InsertSorted(long uid)
        {
            if (!IsReady)
            {
                // bulk load arrives in arbitrary order, sort once instead of shifting the list per insert
                sortedUids.Add(uid);
                unsorted = true;
                return;
            }
            EnsureSorted();
            var n = sortedUids.Count;
            if (n == 0 || sortedUids[n - 1] < uid)
            {
                sortedUids.Add(uid);
                return;
            }
            var index = sortedUids.BinarySearch(uid);
            if (index < 0)
                sortedUids.Insert(~index, uid);
        }

        private void RemoveSorted(long uid)
        {
            if (unsorted)
            {
                sortedUids.Remove(uid);
                return;
            }
            var index = sortedUids.BinarySearch(uid);
            if (index >= 0)
                sortedUids.RemoveAt(index);
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
            bool removed;
            lock (writeLock)
            {
                removed = auctions.TryRemove(uid, out _);
                tombstones[uid] = DateTime.UtcNow;
                if (removed)
                {
                    RemoveSorted(uid);
                    Bump();
                }
            }
            if (!removed)
                return false;
            try { OnRemoved?.Invoke(uid, reason); } catch { }
            return true;
        }

        public bool TryGet(long uid, out SaveAuction a) => auctions.TryGetValue(uid, out a);

        public int ExpireEnded(DateTime now)
        {
            var count = 0;
            foreach (var pair in auctions)
            {
                if (pair.Value.End > now)
                    continue;
                bool removed;
                lock (writeLock)
                {
                    // re-check, the entry may have been replaced by a later end
                    removed = auctions.TryGetValue(pair.Key, out var current) && current.End <= now && auctions.TryRemove(pair.Key, out _);
                    if (removed)
                    {
                        RemoveSorted(pair.Key);
                        Bump();
                    }
                }
                if (removed)
                {
                    count++;
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

        /// <summary>
        /// Rebuilds the snapshot if something changed and the last build is older than <see cref="SnapshotMinInterval"/>.
        /// Readers keep using the previous snapshot meanwhile. The first build and the first one after <see cref="MarkReady"/> are not delayed.
        /// </summary>
        public ActiveAuctionSnapshot RebuildSnapshotIfDirty() => Rebuild(false);

        private ActiveAuctionSnapshot Rebuild(bool ignoreInterval)
        {
            var current = Volatile.Read(ref snapshot);
            if (current.Version == Interlocked.Read(ref version))
                return current;
            if (!ignoreInterval && !IsDue(current))
                return current;
            lock (rebuildLock)
            {
                current = Volatile.Read(ref snapshot);
                var v = Interlocked.Read(ref version);
                if (current.Version == v || (!ignoreInterval && !IsDue(current)))
                    return current;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                forceNextRebuild = false;
                SaveAuction[] all;
                lock (writeLock)
                {
                    v = Interlocked.Read(ref version);
                    EnsureSorted();
                    all = new SaveAuction[sortedUids.Count];
                    for (int p = 0; p < all.Length; p++)
                        all[p] = auctions[sortedUids[p]]; // all mutations hold the write lock, so every uid is present
                }
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
                watch.Stop();
                Interlocked.Exchange(ref lastRebuildTicks, watch.Elapsed.Ticks);
                Interlocked.Increment(ref rebuildCount);
                try { OnSnapshotRebuilt?.Invoke(watch.Elapsed); } catch { }
                return built;
            }
        }

        private bool IsDue(ActiveAuctionSnapshot current)
            => forceNextRebuild || ReferenceEquals(current, ActiveAuctionSnapshot.Empty)
                || DateTime.UtcNow - current.BuiltAt >= SnapshotMinInterval;

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
