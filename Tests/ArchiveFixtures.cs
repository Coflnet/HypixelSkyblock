using System;
using System.Collections.Generic;
using Coflnet.Sky.Core;

namespace Tests
{
    /// <summary>Builds <see cref="SaveAuction"/> fixtures for the archive tests.</summary>
    internal class ArchiveAuctionBuilder
    {
        public const string Tag = "HYPERION";
        private readonly SaveAuction auction;

        public ArchiveAuctionBuilder(long uid, DateTime end)
        {
            auction = new SaveAuction
            {
                Id = (int)uid,
                UId = uid,
                Uuid = Hex(uid),
                Tag = Tag,
                ItemName = "Heroic Hyperion",
                End = DateTime.SpecifyKind(end, DateTimeKind.Unspecified),
                Start = DateTime.SpecifyKind(end.AddDays(-2), DateTimeKind.Unspecified),
                AuctioneerId = Hex(1000 + uid),
                StartingBid = 500,
                Count = 1,
                Tier = Tier.LEGENDARY,
                Category = Category.WEAPON,
                Reforge = ItemReferences.Reforge.None,
                ItemCreatedAt = new DateTime(2021, 3, 4, 5, 6, 7),
                Bids = new List<SaveBids>(),
                Enchantments = new List<Enchantment>(),
                NBTLookup = new List<NBTLookup>(),
            };
        }

        public static string Hex(long value) => value.ToString("x32");

        public ArchiveAuctionBuilder Full()
        {
            auction.ProfileId = Hex(2000 + auction.UId);
            auction.Bin = false;
            auction.Claimed = true;
            auction.Count = 3;
            auction.AnvilUses = 4;
            auction.Reforge = ItemReferences.Reforge.chomp; // negative enum value
            auction.Tier = Tier.MYTHIC;
            auction.HighestBidAmount = 900;
            auction.NbtData = new NbtData { data = new byte[] { 10, 0, 1, 2, 3, 4, 5, 250, 0 } };
            auction.Bids = new List<SaveBids>
            {
                new() { Bidder = Hex(3000), ProfileId = Hex(3001), Amount = 700, Timestamp = new DateTime(2026, 9, 10, 1, 2, 3), BidderId = 31 },
                new() { Bidder = Hex(4000), ProfileId = null, Amount = 900, Timestamp = new DateTime(2026, 9, 10, 1, 5, 3), BidderId = 41 },
                new() { Bidder = Hex(3000), ProfileId = Hex(3001), Amount = 800, Timestamp = new DateTime(2026, 9, 10, 1, 4, 3), BidderId = 31 },
            };
            auction.Enchantments = new List<Enchantment>
            {
                new(Enchantment.EnchantmentType.sharpness, 7),
                new(Enchantment.EnchantmentType.transylvanian, 5), // negative enum value
            };
            auction.NBTLookup = new List<NBTLookup> { new(12, 99), new(3, -5), new(7, long.MaxValue) };
            auction.CoopMembers = new List<UuId> { new(Hex(5000)), new("SOME-Odd-Id") };
            auction.ClaimedBids = new List<UuId> { new(Hex(4000)) };
            return this;
        }

        public ArchiveAuctionBuilder With(Action<SaveAuction> change)
        {
            change(auction);
            return this;
        }

        public SaveAuction Build() => auction;
    }
}
