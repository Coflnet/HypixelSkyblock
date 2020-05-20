using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hypixel {
    [MessagePackObject]
    public class SaveAuction {
        [Key (0)]
        public string Uuid { get; set; }

        [Key (1)]
        public bool Claimed { get; set; }

        [Key (2)]
        public int Count { get; set; }

        [Key (3)]
        public long StartingBid { get; set; }

        [Key (4)]
        public string OldTier { get; set; }

        [Key (5)]
        public string OldCategory;

        [Key (6)]
        public string Tag;
        // [Key (7)]
        //public string ItemLore;
        [Key (8)]
        public string ItemName { get; set; }

        [Key (9)]
        public DateTime Start { get; set; }

        [Key (10)]
        public DateTime End { get; set; }

        [Key (11)]
        public string Auctioneer { get; set; }
        /// <summary>
        /// is <see cref="null"/> if it is the same as the <see cref="Auctioneer"/>
        /// </summary>
        [Key (12)]
        public string ProfileId;
        /// <summary>
        /// All ProfileIds of the coop members without the <see cref="Auctioneer"/> because it would be redundant
        /// </summary>
        [Key (13)]
        public List<string> Coop;
        [Key (14)]
        public List<object> ClaimedBidders;
        [Key (15)]
        public long HighestBidAmount { get; set; }

        [Key (16)]
        public List<SaveBids> Bids;
        [Key (17)]
        public short AnvilUses;
        [Key (18)]
        public List<Enchantment> Enchantments;

        [Key (19)]
        public NbtData NbtData;
        [Key (20)]
        public DateTime ItemCreatedAt;
        [Key (21)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ItemReferences.Reforge Reforge;
        [JsonConverter(typeof(StringEnumConverter))]
        [Key (22)]
        public Category Category;
        [JsonConverter(typeof(StringEnumConverter))]
        [Key(23)]
        public Tier Tier;

        public SaveAuction (Hypixel.NET.SkyblockApi.Auction auction) {
            ClaimedBidders = auction.ClaimedBidders;
            Claimed = auction.Claimed;
            //ItemBytes = auction.ItemBytes;
            StartingBid = auction.StartingBid;
            if (Enum.TryParse (auction.Tier, true, out Tier tier))
                Tier = tier;
            else
                OldTier = auction.Tier;
            if (Enum.TryParse (auction.Category, true, out Category category))
                Category = category;
            else
                OldCategory = auction.Category;
            //ItemLore = auction.ItemLore;
            ItemName = auction.ItemName;
            End = auction.End;
            Start = auction.Start;
            Coop = auction.Coop;
            // remove the Auctioneer Id, because it has to be there anyways
            if (Coop.Remove (auction.Auctioneer))
                Coop.Add (null);

            ProfileId = auction.ProfileId == auction.Auctioneer ? null : auction.ProfileId;
            Auctioneer = auction.Auctioneer;
            Uuid = auction.Uuid;
            HighestBidAmount = auction.HighestBidAmount;
            Bids = new List<SaveBids> ();
            foreach (var item in auction.Bids) {
                Bids.Add (new SaveBids (item));
            }
            NBT.FillDetails (this, auction.ItemBytes);
        }

        public SaveAuction () { }

        public override bool Equals (object obj) {
            return obj is SaveAuction auction &&
                Uuid == auction.Uuid;
        }

        public override int GetHashCode () {
            var hash = new HashCode ();
            hash.Add (Uuid);

            return hash.ToHashCode ();
        }
    }

    public enum Tier {
        UNKNOWN,
        COMMON,
        UNCOMMON,
        RARE,
        EPIC,
        LEGENDARY,
        SPECIAL

    }

    public enum Category {
        UNKNOWN,
        WEAPON,
        ARMOR,
        ACCESSORIES,
        CONSUMABLES,
        BLOCKS,
        MISC

    }
}