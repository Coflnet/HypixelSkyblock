using System;
using System.Collections.Generic;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Immutable point in time view of the <see cref="ActiveAuctionSet"/>. Safe to share between threads.
    /// The indices in <see cref="ByTag"/> and <see cref="BySeller"/> point into <see cref="All"/>.
    /// </summary>
    public class ActiveAuctionSnapshot
    {
        public static readonly ActiveAuctionSnapshot Empty = new(
            Array.Empty<SaveAuction>(),
            new Dictionary<string, int[]>(),
            new Dictionary<string, int[]>(),
            0, DateTime.UtcNow);

        /// <summary>All auctions ordered by UId</summary>
        public SaveAuction[] All { get; }
        public IReadOnlyDictionary<string, int[]> ByTag { get; }
        public IReadOnlyDictionary<string, int[]> BySeller { get; }
        /// <summary>Indices of auctions having the enchantment (any level), key is the stored enchantment type byte</summary>
        public IReadOnlyDictionary<byte, int[]> ByEnchantType { get; }
        public IReadOnlyDictionary<Tier, int[]> ByTier { get; }
        public IReadOnlyDictionary<ItemReferences.Reforge, int[]> ByReforge { get; }
        /// <summary>Indices of bin auctions</summary>
        public int[] BinOnly { get; }
        /// <summary>Indices of auctions that have a color in their flattened nbt</summary>
        public int[] WithColor { get; }
        /// <summary>Sorted indices per parsed <c>NBT.GetColor</c> value of the flattened color entry</summary>
        public IReadOnlyDictionary<long, int[]> ByColorValue { get; }
        public long Version { get; }
        public DateTime BuiltAt { get; }
        public int Count => All.Length;

        public ActiveAuctionSnapshot(SaveAuction[] all, IReadOnlyDictionary<string, int[]> byTag,
            IReadOnlyDictionary<string, int[]> bySeller, long version, DateTime builtAt,
            IReadOnlyDictionary<byte, int[]> byEnchantType = null, IReadOnlyDictionary<Tier, int[]> byTier = null,
            IReadOnlyDictionary<ItemReferences.Reforge, int[]> byReforge = null, int[] binOnly = null, int[] withColor = null,
            IReadOnlyDictionary<long, int[]> byColorValue = null)
        {
            ByEnchantType = byEnchantType ?? new Dictionary<byte, int[]>();
            ByTier = byTier ?? new Dictionary<Tier, int[]>();
            ByReforge = byReforge ?? new Dictionary<ItemReferences.Reforge, int[]>();
            BinOnly = binOnly ?? Array.Empty<int>();
            WithColor = withColor ?? Array.Empty<int>();
            ByColorValue = byColorValue ?? new Dictionary<long, int[]>();
            All = all;
            ByTag = byTag;
            BySeller = bySeller;
            Version = version;
            BuiltAt = builtAt;
        }
    }
}
