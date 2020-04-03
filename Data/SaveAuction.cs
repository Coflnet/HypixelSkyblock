using System;
using System.Collections.Generic;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class SaveAuction {
        [Key (0)]
        public string Uuid {get;set;}
        [Key (1)]
        public bool Claimed {get;set;}
        [Key (2)]
        public int Count { get; set;}
        [Key (3)]
        public long StartingBid { get; set;}
        [Key (4)]
        public string Tier { get; set;}
        [Key (5)]
        public string Category;
        
        [Key (6)]
        public string Tag;
       // [Key (7)]
        //public string ItemLore;
        [Key (8)]
        public string ItemName { get; set;}
        [Key (9)]
        public DateTime Start { get; set;}
        [Key (10)]
        public DateTime End { get; set;}
        [Key (11)]
        public string Auctioneer {get;set;}
        [Key (12)]
        public string ProfileId;
        [Key (13)]
        public List<string> Coop;
        [Key (14)]
        public List<object> ClaimedBidders;
        [Key (15)]
        public long HighestBidAmount { get; set;}
        [Key (16)]
        public List<SaveBids> Bids;
        [Key (17)]
        public short AnvilUses;
        [Key(18)]
        public List<Enchantment> Enchantments;

        [Key(19)]
        public NbtData NbtData;
        [Key(20)]
        public DateTime ItemCreatedAt;


        public SaveAuction (Hypixel.NET.SkyblockApi.Auction auction) {
            ClaimedBidders = auction.ClaimedBidders;
            Claimed = auction.Claimed;
            //ItemBytes = auction.ItemBytes;
            StartingBid = auction.StartingBid;
            Tier = auction.Tier;
            Category = auction.Category;
            //ItemLore = auction.ItemLore;
            ItemName = auction.ItemName;
            End = auction.End;
            Start = auction.Start;
            Coop = auction.Coop;
            ProfileId = auction.ProfileId;
            Auctioneer = auction.Auctioneer;
            Uuid = auction.Uuid;
            HighestBidAmount = auction.HighestBidAmount;
            Bids = new List<SaveBids> ();
            foreach (var item in auction.Bids) {
                Bids.Add (new SaveBids (item));
            }
            NBT.FillDetails(this,auction.ItemBytes);
            NbtData = new NbtData(auction.ItemBytes);
        }

        public SaveAuction () { }

        public override bool Equals(object obj)
        {
            return obj is SaveAuction auction &&
                   Uuid == auction.Uuid;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Uuid);
            
            return hash.ToHashCode();
        }
    }
}