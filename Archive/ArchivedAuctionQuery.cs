using System;
using System.Collections.Generic;
using System.Linq;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>One archived auction read back by <see cref="ArchivedAuctionReader"/>.</summary>
    public sealed class ArchivedAuction
    {
        private IReadOnlyDictionary<string, string> nbtStrings;

        /// <summary>The row (database ids are not restored; collections are never null).</summary>
        public SaveAuction Row { get; init; }
        /// <summary>Scylla-only columns of the row, null when the block has none (MariaDB blocks).</summary>
        public ScyllaAuctionExtras Extras { get; init; }
        /// <summary>Month of the block, <c>yyyyMM</c>.</summary>
        public int Month { get; init; }
        /// <summary>Part of the tag-month the row was read from.</summary>
        public int Part { get; init; }
        /// <summary>Source of the block (<see cref="ArchivedAuctionBlock.SourceMariaDb"/>, <see cref="ArchivedAuctionBlock.SourceScylla"/>, <see cref="ArchivedAuctionBlock.SourceMerged"/>).</summary>
        public string Source { get; init; }

        /// <summary>Sold flag as archived: highest bid above 0.</summary>
        public bool Sold => Row.HighestBidAmount > 0;

        /// <summary>Bidder uuid of the highest bid (the block's <c>HighestBidder</c> column), null without bids.</summary>
        public string HighestBidder => ArchivedAuctionBlock.HighestBid(Row.Bids)?.Bidder;

        /// <summary>
        /// String-keyed flattened nbt of the row: Scylla's <c>nbtlookup</c> (<see cref="ScyllaAuctionExtras.NbtLookupStrings"/>) when the row has
        /// a non-empty one, otherwise flattened from the archived item bytes with the same helpers Scylla's map was built with
        /// (<see cref="NBT.FlattenNbtData"/>, <see cref="SaveAuction.SetFlattenedNbt"/>). Empty (never null) when neither exists or the
        /// item bytes can not be parsed. Computed once per instance.
        /// </summary>
        public IReadOnlyDictionary<string, string> NbtStrings => nbtStrings ??= ResolveNbtStrings(Row, Extras);

        internal static IReadOnlyDictionary<string, string> ResolveNbtStrings(SaveAuction row, ScyllaAuctionExtras extras)
        {
            if (extras?.NbtLookupStrings?.Count > 0)
                return extras.NbtLookupStrings;
            var bytes = row?.NbtData?.data;
            if (bytes == null || bytes.Length == 0)
                return EmptyMap;
            try
            {
                var data = row.NbtData.Data;
                if (data == null || data.Count == 0)
                    return EmptyMap;
                row.SetFlattenedNbt(NBT.FlattenNbtData(data));
                return row.FlatenedNBT ?? (IReadOnlyDictionary<string, string>)EmptyMap;
            }
            catch (Exception)
            {
                // unparsable item bytes: no lookup instead of failing the whole read
                return EmptyMap;
            }
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyMap = new Dictionary<string, string>();
    }

    /// <summary>
    /// Filter of <see cref="ArchivedAuctionReader.ReadMonthAsync"/>. Every set field must match (AND); unset fields match everything.
    /// <see cref="Matches(SaveAuction, ScyllaAuctionExtras)"/> is pure apart from parsing the item bytes for <see cref="NbtLookup"/>.
    /// </summary>
    public sealed class ArchivedAuctionQuery
    {
        /// <summary>End at or after this instant (UTC; Unspecified is treated as UTC).</summary>
        public DateTime? EndFrom { get; set; }
        /// <summary>End before this instant (exclusive; UTC; Unspecified is treated as UTC).</summary>
        public DateTime? EndTo { get; set; }
        /// <summary>Sold (highest bid above 0) or not.</summary>
        public bool? Sold { get; set; }
        /// <summary>BIN or auction.</summary>
        public bool? Bin { get; set; }
        /// <summary>
        /// Minimum price (inclusive). The price of a row is <see cref="SaveAuction.HighestBidAmount"/> when it sold and
        /// <see cref="SaveAuction.StartingBid"/> otherwise (see <see cref="PriceOf"/>).
        /// </summary>
        public long? MinPrice { get; set; }
        /// <summary>Maximum price (inclusive), same price rule as <see cref="MinPrice"/>.</summary>
        public long? MaxPrice { get; set; }
        /// <summary>Seller uuid (hex, dashes and case are ignored).</summary>
        public string Auctioneer { get; set; }
        /// <summary>Substring of the item name (ordinal, ignore case).</summary>
        public string ItemNameContains { get; set; }
        /// <summary>
        /// Every entry must be present with exactly this value (ordinal) in the row's string-keyed flattened nbt
        /// (<see cref="ArchivedAuction.NbtStrings"/>: Scylla's <c>nbtlookup</c> when present, otherwise flattened from the item bytes).
        /// MariaDB's short-keyed <c>NBTLookups</c> are not used: their key ids live in the <c>NBTKeys</c> table and the values are
        /// type dependent ids, so there is no pure mapping.
        /// </summary>
        public IReadOnlyDictionary<string, string> NbtLookup { get; set; }
        /// <summary>Maximum number of rows returned; null = all.</summary>
        public int? Limit { get; set; }
        /// <summary>Newest End first instead of oldest first.</summary>
        public bool Descending { get; set; }

        /// <summary>Price the min/max filters use: highest bid when sold, starting bid otherwise.</summary>
        public static long PriceOf(SaveAuction row) => row.HighestBidAmount > 0 ? row.HighestBidAmount : row.StartingBid;

        /// <summary>True if the row passes every set filter (<see cref="Limit"/> and <see cref="Descending"/> are not filters).</summary>
        public bool Matches(SaveAuction row, ScyllaAuctionExtras extras) => Matches(row, extras, null);

        /// <summary>Same as <see cref="Matches(SaveAuction, ScyllaAuctionExtras)"/> on an <see cref="ArchivedAuction"/> (reuses its cached nbt map).</summary>
        public bool Matches(ArchivedAuction auction) => auction != null && Matches(auction.Row, auction.Extras, auction);

        private bool Matches(SaveAuction row, ScyllaAuctionExtras extras, ArchivedAuction cachedNbt)
        {
            if (row == null)
                return false;
            if (EndFrom != null && row.End < AsUtc(EndFrom.Value))
                return false;
            if (EndTo != null && row.End >= AsUtc(EndTo.Value))
                return false;
            if (Sold != null && (row.HighestBidAmount > 0) != Sold.Value)
                return false;
            if (Bin != null && row.Bin != Bin.Value)
                return false;
            if (MinPrice != null || MaxPrice != null)
            {
                var price = PriceOf(row);
                if (MinPrice != null && price < MinPrice.Value)
                    return false;
                if (MaxPrice != null && price > MaxPrice.Value)
                    return false;
            }
            if (!string.IsNullOrEmpty(Auctioneer) && !string.Equals(NormalizeUuid(row.AuctioneerId), NormalizeUuid(Auctioneer), StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(ItemNameContains) && (row.ItemName == null || row.ItemName.IndexOf(ItemNameContains, StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            if (NbtLookup != null && NbtLookup.Count > 0)
            {
                var map = cachedNbt?.NbtStrings ?? ArchivedAuction.ResolveNbtStrings(row, extras);
                foreach (var kv in NbtLookup)
                {
                    if (!map.TryGetValue(kv.Key, out var value) || !string.Equals(value, kv.Value, StringComparison.Ordinal))
                        return false;
                }
            }
            return true;
        }

        /// <summary>Lower case hex without dashes; null stays null.</summary>
        public static string NormalizeUuid(string uuid) => uuid?.Trim().Replace("-", "").ToLowerInvariant();

        internal static DateTime AsUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        /// <summary>True if no row of the month <c>[start, end)</c> can match the End filters.</summary>
        internal bool ExcludesRange(DateTime start, DateTime end)
            => EndFrom != null && AsUtc(EndFrom.Value) >= end || EndTo != null && AsUtc(EndTo.Value) <= start;
    }
}
