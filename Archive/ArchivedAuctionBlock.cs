using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// One bid inside <see cref="ArchivedAuctionBlock.SerialisedBids"/>. Database ids are not archived.
    /// </summary>
    [MessagePackObject]
    public class ArchivedBid
    {
        /// <summary>Bidder uuid, encoded with <see cref="ArchiveIds"/>.</summary>
        [Key(0)] public byte[] Bidder { get; set; }
        /// <summary>Bidder profile uuid, encoded with <see cref="ArchiveIds"/>.</summary>
        [Key(1)] public byte[] ProfileId { get; set; }
        /// <summary>Bid amount.</summary>
        [Key(2)] public long Amount { get; set; }
        /// <summary>UTC ticks of the bid.</summary>
        [Key(3)] public long TimestampTicks { get; set; }
    }

    /// <summary>
    /// Column-oriented, MessagePack-serialisable, immutable archive of the auctions of one tag that ended in one
    /// UTC month (one part of at most a configured number of rows). Brotli is applied on top when stored.
    /// Rows are sorted by End, then auction uid. Index i of every row column belongs to row i.
    /// Enums are stored as their numeric value. Columns that a source does not have are null as a whole.
    /// Adding a new nullable key later is backwards compatible; changing or removing one requires a new <see cref="CurrentFormatVersion"/>.
    /// </summary>
    [MessagePackObject]
    public class ArchivedAuctionBlock
    {
        /// <summary>Format version written by <see cref="FromAuctions"/>.</summary>
        public const int CurrentFormatVersion = 2;
        /// <summary>Block built from MariaDB rows.</summary>
        public const string SourceMariaDb = "mariadb";
        /// <summary>Block built from Scylla rows.</summary>
        public const string SourceScylla = "scylla";
        /// <summary>Block built from MariaDB rows plus Scylla-only rows.</summary>
        public const string SourceMerged = "merged";

        /// <summary>Blob format version.</summary>
        [Key(0)] public int FormatVersion { get; set; } = CurrentFormatVersion;
        /// <summary>Item tag (as stored on the auction, max 40 chars).</summary>
        [Key(1)] public string Tag { get; set; }
        /// <summary>Month the auctions ended in, <c>yyyyMM</c>.</summary>
        [Key(2)] public int Month { get; set; }
        /// <summary>Part number within the month (0-based).</summary>
        [Key(3)] public int Part { get; set; }
        /// <summary><see cref="SourceMariaDb"/>, <see cref="SourceScylla"/> or <see cref="SourceMerged"/>.</summary>
        [Key(4)] public string Source { get; set; }
        /// <summary>UTC ticks of the first (earliest) End; <see cref="EndDeltasMs"/> are relative to this.</summary>
        [Key(5)] public long FirstEndTicks { get; set; }
        /// <summary>Per-row End minus <see cref="FirstEndTicks"/> in milliseconds (long: a month exceeds int ms).</summary>
        [Key(6)] public long[] EndDeltasMs { get; set; }
        /// <summary>Per-row End minus Start in milliseconds.</summary>
        [Key(7)] public long[] StartOffsetMs { get; set; }
        /// <summary>Per-row auction uuid (<see cref="ArchiveIds"/>).</summary>
        [Key(8)] public byte[][] Uuid { get; set; }
        /// <summary>Per-row <see cref="SaveAuction.UId"/>.</summary>
        [Key(9)] public long[] AuctionUid { get; set; }
        /// <summary>Per-row sold flag (MariaDB source: highest bid amount above 0).</summary>
        [Key(10)] public bool[] IsSold { get; set; }
        /// <summary>Per-row BIN flag.</summary>
        [Key(11)] public bool[] Bin { get; set; }
        /// <summary>Per-row <see cref="SaveAuction.Claimed"/>.</summary>
        [Key(12)] public bool[] Claimed { get; set; }
        /// <summary>Per-row seller uuid.</summary>
        [Key(13)] public byte[][] Auctioneer { get; set; }
        /// <summary>Per-row seller profile uuid (null when the same as the seller, as in MariaDB).</summary>
        [Key(14)] public byte[][] ProfileId { get; set; }
        /// <summary>Per-row highest bidder uuid, null without bids.</summary>
        [Key(15)] public byte[][] HighestBidder { get; set; }
        /// <summary>Per-row highest bid.</summary>
        [Key(16)] public long[] HighestBidAmount { get; set; }
        /// <summary>Per-row starting bid.</summary>
        [Key(17)] public long[] StartingBid { get; set; }
        /// <summary>Per-row item count.</summary>
        [Key(18)] public int[] Count { get; set; }
        /// <summary>Per-row <see cref="Core.Tier"/> value.</summary>
        [Key(19)] public byte[] Tier { get; set; }
        /// <summary>Per-row <see cref="Core.Category"/> value.</summary>
        [Key(20)] public byte[] Category { get; set; }
        /// <summary>Per-row reforge value as two's complement byte (the enum has negative members).</summary>
        [Key(21)] public byte[] Reforge { get; set; }
        /// <summary>Per-row anvil uses.</summary>
        [Key(22)] public short[] AnvilUses { get; set; }
        /// <summary>Per-row item name.</summary>
        [Key(23)] public string[] ItemName { get; set; }
        /// <summary>Per-row item creation UTC ticks.</summary>
        [Key(24)] public long[] ItemCreatedAtTicks { get; set; }
        /// <summary>Per-row raw item bytes (MariaDB <c>NbtData.data</c>, Scylla <c>itembytes</c>).</summary>
        [Key(25)] public byte[][] ItemBytes { get; set; }
        /// <summary>Per-row MessagePack <c>List&lt;ArchivedBid&gt;</c>, null without bids.</summary>
        [Key(26)] public byte[][] SerialisedBids { get; set; }
        /// <summary>Per-row MessagePack <c>List&lt;string&gt;</c> of coop member uuids, null when none.</summary>
        [Key(27)] public byte[][] Coop { get; set; }
        /// <summary>Per-row MessagePack <c>List&lt;string&gt;</c> of claimed bidder uuids, null when none.</summary>
        [Key(28)] public byte[][] ClaimedBidders { get; set; }
        /// <summary>Per-row MessagePack <c>Dictionary&lt;short,long&gt;</c> keyId to value (MariaDB <c>NBTLookups</c>), sorted by key, null when none.</summary>
        [Key(29)] public byte[][] NbtLookup { get; set; }
        /// <summary>Per-row MessagePack array of (type, level) pairs, null when none.</summary>
        [Key(30)] public byte[][] Enchantments { get; set; }
        /// <summary>Scylla only: per-row highest bidder name.</summary>
        [Key(31)] public string[] HighestBidderName { get; set; }
        /// <summary>Scylla only: per-row seller profile name.</summary>
        [Key(32)] public string[] ProfileName { get; set; }
        /// <summary>Scylla only: per-row coop name.</summary>
        [Key(33)] public string[] CoopName { get; set; }
        /// <summary>Scylla only: per-row item color.</summary>
        [Key(34)] public int?[] Color { get; set; }
        /// <summary>Scylla only: per-row item uid.</summary>
        [Key(35)] public long[] ItemUid { get; set; }
        /// <summary>Scylla only: per-row item uuid (16 bytes).</summary>
        [Key(36)] public byte[][] ItemId { get; set; }
        /// <summary>Scylla only: per-row MessagePack <c>Dictionary&lt;string,string&gt;</c> of the flattened nbt lookup.</summary>
        [Key(37)] public byte[][] NbtLookupStrings { get; set; }

        /// <summary>Number of rows.</summary>
        [IgnoreMember]
        public int RowCount => EndDeltasMs?.Length ?? 0;

        /// <summary>Month of an End instant, <c>yyyyMM</c>.</summary>
        public static int MonthOf(DateTime endUtc) => ArchiveMonths.MonthOf(endUtc);

        /// <summary>UTC start (inclusive) and end (exclusive) of a month.</summary>
        public static (DateTime start, DateTime end) MonthRange(int month) => ArchiveMonths.MonthRange(month);

        /// <summary>
        /// Pack the auctions of one tag-month part. Rows are sorted by End, then UId.
        /// Throws if a row belongs to another tag or month, or carries sub-millisecond times (would not round trip).
        /// </summary>
        public static ArchivedAuctionBlock FromAuctions(string tag, int month, int part, string source, IEnumerable<SaveAuction> rows)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentException("Tag is required", nameof(tag));
            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Source is required", nameof(source));
            if (part < 0)
                throw new ArgumentOutOfRangeException(nameof(part));
            var (monthStart, _) = ArchiveMonths.MonthRange(month);
            var list = (rows ?? Enumerable.Empty<SaveAuction>()).ToList();
            foreach (var r in list)
            {
                if (r.Tag != tag)
                    throw new ArgumentException($"Auction {r.Uuid} has tag {r.Tag}, block is {tag}", nameof(rows));
                if (ArchiveMonths.MonthOf(r.End) != month)
                    throw new ArgumentException($"Auction {r.Uuid} ended {r.End:O}, outside month {month}", nameof(rows));
                if (r.End.Ticks % TimeSpan.TicksPerMillisecond != 0 || r.Start.Ticks % TimeSpan.TicksPerMillisecond != 0)
                    throw new ArgumentException($"Auction {r.Uuid} has sub-millisecond start/end", nameof(rows));
            }
            list.Sort((a, b) =>
            {
                var c = a.End.Ticks.CompareTo(b.End.Ticks);
                return c != 0 ? c : a.UId.CompareTo(b.UId);
            });

            var n = list.Count;
            var first = n == 0 ? monthStart.Ticks : list[0].End.Ticks;
            var block = new ArchivedAuctionBlock
            {
                FormatVersion = CurrentFormatVersion,
                Tag = tag,
                Month = month,
                Part = part,
                Source = source,
                FirstEndTicks = first,
                EndDeltasMs = new long[n],
                StartOffsetMs = new long[n],
                Uuid = new byte[n][],
                AuctionUid = new long[n],
                IsSold = new bool[n],
                Bin = new bool[n],
                Claimed = new bool[n],
                Auctioneer = new byte[n][],
                ProfileId = new byte[n][],
                HighestBidder = new byte[n][],
                HighestBidAmount = new long[n],
                StartingBid = new long[n],
                Count = new int[n],
                Tier = new byte[n],
                Category = new byte[n],
                Reforge = new byte[n],
                AnvilUses = new short[n],
                ItemName = new string[n],
                ItemCreatedAtTicks = new long[n],
                ItemBytes = new byte[n][],
                SerialisedBids = new byte[n][],
                Coop = new byte[n][],
                ClaimedBidders = new byte[n][],
                NbtLookup = new byte[n][],
                Enchantments = new byte[n][],
            };
            for (var i = 0; i < n; i++)
            {
                var r = list[i];
                block.EndDeltasMs[i] = (r.End.Ticks - first) / TimeSpan.TicksPerMillisecond;
                block.StartOffsetMs[i] = (r.End.Ticks - r.Start.Ticks) / TimeSpan.TicksPerMillisecond;
                block.Uuid[i] = ArchiveIds.Encode(r.Uuid);
                block.AuctionUid[i] = r.UId;
                block.IsSold[i] = r.HighestBidAmount > 0;
                block.Bin[i] = r.Bin;
                block.Claimed[i] = r.Claimed;
                block.Auctioneer[i] = ArchiveIds.Encode(r.AuctioneerId);
                block.ProfileId[i] = ArchiveIds.Encode(r.ProfileId);
                block.HighestBidder[i] = ArchiveIds.Encode(HighestBid(r.Bids)?.Bidder);
                block.HighestBidAmount[i] = r.HighestBidAmount;
                block.StartingBid[i] = r.StartingBid;
                block.Count[i] = r.Count;
                block.Tier[i] = checked((byte)(int)r.Tier);
                block.Category[i] = checked((byte)(int)r.Category);
                block.Reforge[i] = unchecked((byte)checked((sbyte)(int)r.Reforge));
                block.AnvilUses[i] = r.AnvilUses;
                block.ItemName[i] = r.ItemName;
                block.ItemCreatedAtTicks[i] = r.ItemCreatedAt.Ticks;
                block.ItemBytes[i] = r.NbtData?.data;
                block.SerialisedBids[i] = SerialiseBids(r.Bids);
                block.Coop[i] = SerialiseIds(r.CoopMembers);
                block.ClaimedBidders[i] = SerialiseIds(r.ClaimedBids);
                block.NbtLookup[i] = SerialiseLookup(r.NBTLookup, r.Uuid);
                block.Enchantments[i] = SerialiseEnchantments(r.Enchantments);
            }
            return block;
        }

        /// <summary>The bid that determines the highest bidder: highest amount, first in list order on ties.</summary>
        public static SaveBids HighestBid(IEnumerable<SaveBids> bids)
        {
            SaveBids best = null;
            if (bids == null)
                return null;
            foreach (var b in bids)
                if (best == null || b.Amount > best.Amount)
                    best = b;
            return best;
        }

        /// <summary>Materialise the rows back into <see cref="SaveAuction"/>s (database ids are not restored; collections are never null).</summary>
        public IEnumerable<SaveAuction> ToAuctions()
        {
            var n = RowCount;
            for (var i = 0; i < n; i++)
            {
                var endTicks = FirstEndTicks + EndDeltasMs[i] * TimeSpan.TicksPerMillisecond;
                var uuid = ArchiveIds.Decode(Uuid[i]);
                var auction = new SaveAuction
                {
                    Uuid = uuid,
                    UId = AuctionUid[i],
                    Tag = Tag,
                    End = new DateTime(endTicks, DateTimeKind.Utc),
                    Start = new DateTime(endTicks - StartOffsetMs[i] * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc),
                    Bin = Bin[i],
                    Claimed = Claimed != null && Claimed[i],
                    AuctioneerId = ArchiveIds.Decode(Auctioneer[i]),
                    ProfileId = ArchiveIds.Decode(ProfileId[i]),
                    HighestBidAmount = HighestBidAmount[i],
                    StartingBid = StartingBid[i],
                    Count = Count[i],
                    Tier = (Tier)Tier[i],
                    Category = (Category)Category[i],
                    Reforge = (ItemReferences.Reforge)unchecked((sbyte)Reforge[i]),
                    AnvilUses = AnvilUses[i],
                    ItemName = ItemName[i],
                    ItemCreatedAt = new DateTime(ItemCreatedAtTicks[i], DateTimeKind.Utc),
                    NbtData = ItemBytes[i] == null ? null : new NbtData { data = ItemBytes[i] },
                    Bids = DeserialiseBids(SerialisedBids[i], uuid),
                    CoopMembers = DeserialiseIds(Coop?[i]),
                    ClaimedBids = DeserialiseIds(ClaimedBidders?[i]),
                    NBTLookup = DeserialiseLookup(NbtLookup?[i]),
                    Enchantments = DeserialiseEnchantments(Enchantments?[i]),
                };
                yield return auction;
            }
        }

        /// <summary>True if every uid is a row of this block.</summary>
        public bool CoversUids(IEnumerable<long> uids)
        {
            if (uids == null)
                return true;
            var have = new HashSet<long>(AuctionUid ?? Array.Empty<long>());
            return uids.All(have.Contains);
        }

        /// <summary>Structural equality of every column, used to verify a read-back block against the one written.</summary>
        public bool SameRows(ArchivedAuctionBlock other) => FirstDifference(other) == null;

        /// <summary>Name of the first column that differs from <paramref name="other"/>, or null when all are equal.</summary>
        public string FirstDifference(ArchivedAuctionBlock other)
        {
            if (other == null)
                return "null";
            if (RowCount != other.RowCount) return nameof(RowCount);
            if (FormatVersion != other.FormatVersion) return nameof(FormatVersion);
            if (Tag != other.Tag) return nameof(Tag);
            if (Month != other.Month) return nameof(Month);
            if (Part != other.Part) return nameof(Part);
            if (Source != other.Source) return nameof(Source);
            if (FirstEndTicks != other.FirstEndTicks) return nameof(FirstEndTicks);
            if (!ArrayEq(EndDeltasMs, other.EndDeltasMs)) return nameof(EndDeltasMs);
            if (!ArrayEq(StartOffsetMs, other.StartOffsetMs)) return nameof(StartOffsetMs);
            if (!JaggedEq(Uuid, other.Uuid)) return nameof(Uuid);
            if (!ArrayEq(AuctionUid, other.AuctionUid)) return nameof(AuctionUid);
            if (!ArrayEq(IsSold, other.IsSold)) return nameof(IsSold);
            if (!ArrayEq(Bin, other.Bin)) return nameof(Bin);
            if (!ArrayEq(Claimed, other.Claimed)) return nameof(Claimed);
            if (!JaggedEq(Auctioneer, other.Auctioneer)) return nameof(Auctioneer);
            if (!JaggedEq(ProfileId, other.ProfileId)) return nameof(ProfileId);
            if (!JaggedEq(HighestBidder, other.HighestBidder)) return nameof(HighestBidder);
            if (!ArrayEq(HighestBidAmount, other.HighestBidAmount)) return nameof(HighestBidAmount);
            if (!ArrayEq(StartingBid, other.StartingBid)) return nameof(StartingBid);
            if (!ArrayEq(Count, other.Count)) return nameof(Count);
            if (!ArrayEq(Tier, other.Tier)) return nameof(Tier);
            if (!ArrayEq(Category, other.Category)) return nameof(Category);
            if (!ArrayEq(Reforge, other.Reforge)) return nameof(Reforge);
            if (!ArrayEq(AnvilUses, other.AnvilUses)) return nameof(AnvilUses);
            if (!ArrayEq(ItemName, other.ItemName)) return nameof(ItemName);
            if (!ArrayEq(ItemCreatedAtTicks, other.ItemCreatedAtTicks)) return nameof(ItemCreatedAtTicks);
            if (!JaggedEq(ItemBytes, other.ItemBytes)) return nameof(ItemBytes);
            if (!JaggedEq(SerialisedBids, other.SerialisedBids)) return nameof(SerialisedBids);
            if (!JaggedEq(Coop, other.Coop)) return nameof(Coop);
            if (!JaggedEq(ClaimedBidders, other.ClaimedBidders)) return nameof(ClaimedBidders);
            if (!JaggedEq(NbtLookup, other.NbtLookup)) return nameof(NbtLookup);
            if (!JaggedEq(Enchantments, other.Enchantments)) return nameof(Enchantments);
            if (!ArrayEq(HighestBidderName, other.HighestBidderName)) return nameof(HighestBidderName);
            if (!ArrayEq(ProfileName, other.ProfileName)) return nameof(ProfileName);
            if (!ArrayEq(CoopName, other.CoopName)) return nameof(CoopName);
            if (!ArrayEq(Color, other.Color)) return nameof(Color);
            if (!ArrayEq(ItemUid, other.ItemUid)) return nameof(ItemUid);
            if (!JaggedEq(ItemId, other.ItemId)) return nameof(ItemId);
            if (!JaggedEq(NbtLookupStrings, other.NbtLookupStrings)) return nameof(NbtLookupStrings);
            return null;
        }

        /// <summary>Approximate decoded in-memory size, used to bill the in-process cache and to size parts.</summary>
        public long EstimatedBytes()
        {
            const int arrayHeader = 24;
            // fixed width columns: 6 longs, 3 bools, int, 3 bytes, short; plus a slot reference for each of the 15 jagged/string columns
            const int perRowFixed = 6 * 8 + 3 + 4 + 3 + 2 + 15 * 8;
            long total = 512 + (long)RowCount * perRowFixed;
            foreach (var column in new[] { Uuid, Auctioneer, ProfileId, HighestBidder, ItemBytes, SerialisedBids, Coop, ClaimedBidders, NbtLookup, Enchantments, ItemId, NbtLookupStrings })
            {
                if (column == null)
                    continue;
                foreach (var b in column)
                    if (b != null)
                        total += arrayHeader + b.Length;
            }
            foreach (var column in new[] { ItemName, HighestBidderName, ProfileName, CoopName })
            {
                if (column == null)
                    continue;
                foreach (var s in column)
                    if (s != null)
                        total += arrayHeader + 2L * s.Length;
            }
            return total;
        }

        /// <summary>Lowercase hex SHA-256 of the (uncompressed) MessagePack serialisation of this block.</summary>
        public string Sha256()
        {
            using var sha = SHA256.Create();
            using (var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            {
                MessagePackSerializer.Serialize(crypto, this);
                crypto.FlushFinalBlock();
            }
            return Convert.ToHexString(sha.Hash).ToLowerInvariant();
        }

        /// <summary>
        /// Hash of a tag-month made of several parts: the part hash itself for one part, otherwise
        /// the SHA-256 of the concatenated lowercase part hashes in part order.
        /// </summary>
        public static string CombinedSha256(IReadOnlyList<string> partHashes)
        {
            if (partHashes != null && partHashes.Count == 1)
                return partHashes[0];
            var joined = string.Concat(partHashes ?? Array.Empty<string>());
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
        }

        private static byte[] SerialiseBids(List<SaveBids> bids)
        {
            if (bids == null || bids.Count == 0)
                return null;
            var archived = new List<ArchivedBid>(bids.Count);
            foreach (var b in bids)
            {
                archived.Add(new ArchivedBid
                {
                    Bidder = ArchiveIds.Encode(b.Bidder),
                    ProfileId = ArchiveIds.Encode(b.ProfileId),
                    Amount = b.Amount,
                    TimestampTicks = b.Timestamp.Ticks,
                });
            }
            return MessagePackSerializer.Serialize(archived);
        }

        private static List<SaveBids> DeserialiseBids(byte[] bytes, string auctionUuid)
        {
            if (bytes == null)
                return new List<SaveBids>();
            return MessagePackSerializer.Deserialize<List<ArchivedBid>>(bytes).Select(b => new SaveBids
            {
                AuctionId = auctionUuid,
                Bidder = ArchiveIds.Decode(b.Bidder),
                ProfileId = ArchiveIds.Decode(b.ProfileId),
                Amount = b.Amount,
                Timestamp = new DateTime(b.TimestampTicks, DateTimeKind.Utc),
            }).ToList();
        }

        private static byte[] SerialiseIds(List<UuId> ids)
        {
            if (ids == null || ids.Count == 0)
                return null;
            return MessagePackSerializer.Serialize(ids.Select(i => i.value).ToList());
        }

        private static List<UuId> DeserialiseIds(byte[] bytes)
        {
            if (bytes == null)
                return new List<UuId>();
            return MessagePackSerializer.Deserialize<List<string>>(bytes).Select(v => new UuId(v)).ToList();
        }

        private static byte[] SerialiseLookup(IEnumerable<NBTLookup> lookups, string uuid)
        {
            if (lookups == null)
                return null;
            var sorted = new SortedDictionary<short, long>();
            foreach (var l in lookups)
            {
                if (!sorted.TryAdd(l.KeyId, l.Value))
                    throw new ArgumentException($"Auction {uuid} has duplicate nbt lookup key {l.KeyId}");
            }
            if (sorted.Count == 0)
                return null;
            var ordered = new Dictionary<short, long>(sorted.Count);
            foreach (var kv in sorted)
                ordered.Add(kv.Key, kv.Value);
            return MessagePackSerializer.Serialize(ordered);
        }

        private static List<NBTLookup> DeserialiseLookup(byte[] bytes)
        {
            if (bytes == null)
                return new List<NBTLookup>();
            return MessagePackSerializer.Deserialize<Dictionary<short, long>>(bytes)
                .OrderBy(kv => kv.Key)
                .Select(kv => new NBTLookup(kv.Key, kv.Value)).ToList();
        }

        private static byte[] SerialiseEnchantments(List<Enchantment> enchantments)
        {
            if (enchantments == null || enchantments.Count == 0)
                return null;
            var pairs = new int[enchantments.Count][];
            for (var i = 0; i < pairs.Length; i++)
                pairs[i] = new[] { (int)enchantments[i].Type, enchantments[i].Level };
            return MessagePackSerializer.Serialize(pairs);
        }

        private static List<Enchantment> DeserialiseEnchantments(byte[] bytes)
        {
            if (bytes == null)
                return new List<Enchantment>();
            return MessagePackSerializer.Deserialize<int[][]>(bytes)
                .Select(p => new Enchantment((Enchantment.EnchantmentType)p[0], checked((byte)p[1])))
                .ToList();
        }

        private static bool ArrayEq<T>(T[] a, T[] b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            if (a.Length != b.Length)
                return false;
            var cmp = EqualityComparer<T>.Default;
            for (var i = 0; i < a.Length; i++)
                if (!cmp.Equals(a[i], b[i]))
                    return false;
            return true;
        }

        private static bool JaggedEq(byte[][] a, byte[][] b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] == null || b[i] == null)
                {
                    if (a[i] != b[i])
                        return false;
                    continue;
                }
                if (!a[i].AsSpan().SequenceEqual(b[i]))
                    return false;
            }
            return true;
        }
    }
}
