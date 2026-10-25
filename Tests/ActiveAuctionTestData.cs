using System;
using System.Collections.Generic;
using Coflnet.Sky.Core;

namespace Tests
{
    internal static class ActiveAuctionTestData
    {
        public static SaveAuction Make(long uid, int bids = 0, string tag = "ASPECT_OF_THE_END", string seller = null, DateTime? end = null, int itemNameLen = 20)
        {
            var seller32 = seller ?? ("seller" + (uid % 7)).PadRight(32, 'a');
            var a = new SaveAuction
            {
                Uuid = uid.ToString("x").PadLeft(32, '0'),
                UId = uid,
                Tag = new string(tag.ToCharArray()),
                ItemName = new string('n', itemNameLen) + uid % 5,
                Start = DateTime.UtcNow.AddMinutes(-5),
                End = end ?? DateTime.UtcNow.AddHours(1),
                StartingBid = 1000 + uid,
                HighestBidAmount = bids * 1500,
                Bin = uid % 2 == 0,
                Tier = Tier.EPIC,
                Category = Category.WEAPON,
                Reforge = ItemReferences.Reforge.Sharp,
                Count = 1,
                AuctioneerId = new string(seller32.ToCharArray()),
                ProfileId = new string(seller32.ToCharArray()),
                ItemId = (int)(uid % 1000) + 1,
                Enchantments = new List<Enchantment> { new Enchantment(Enchantment.EnchantmentType.sharpness, 5, 3) },
                Bids = new List<SaveBids>(),
                NbtData = new NbtData { data = new byte[118] },
                Context = new Dictionary<string, string> { { "x", "y" } },
            };
            for (int i = 0; i < bids; i++)
                a.Bids.Add(new SaveBids { Bidder = "bidder".PadRight(32, 'b'), Amount = (i + 1) * 1500, Timestamp = DateTime.UtcNow.AddMinutes(-i) });
            a.FlatenedNBT = new Dictionary<string, string>
            {
                [new string("uuid".ToCharArray())] = Guid.NewGuid().ToString(),
                [new string("rarity_upgrades".ToCharArray())] = "1",
                [new string("hot_potato_count".ToCharArray())] = "10",
            };
            return a;
        }
    }
}
