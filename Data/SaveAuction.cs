using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hypixel
{

    [MessagePackObject]
    public class SaveAuction {
        [IgnoreMember]
        public int Id {get;set;}
        [Key (0)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string Uuid { get; set; }

        [Key (1)]
        public bool Claimed { get; set; }

        [Key (2)]
        public int Count { get; set; }

        [Key (3)]
        public long StartingBid { get; set; }

        [Key (4)]
        public string OldTier { set {
            if(value == null)
                return;
            Tier =  (Tier)Enum.Parse(typeof(Tier),value,true);
        } }


        [Key (5)]
        public string OldCategory {set{
            if(value == null)
                return;
            Category =  (Category)Enum.Parse(typeof(Category),value,true);
        }}

        [Key (6)]
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        public string Tag {get; set;}
        // [Key (7)]
        //public string ItemLore;

        private string _itemName;
        [Key (8)]
        [System.ComponentModel.DataAnnotations.MaxLength(45)]
        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        public string ItemName { get {return _itemName;} set {_itemName=value?.Substring(0, Math.Min(value.Length, 45));} }

        [Key (9)]
        public DateTime Start { get; set; }

        [Key (10)]
        public DateTime End { get; set; }

        [Key (11)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string AuctioneerId { get; set; }
    /*    [IgnoreMember]
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("AuctioneerId")]
        public Player Auctioneer {get;set;} */

        /// <summary>
        /// is <see cref="null"/> if it is the same as the <see cref="Auctioneer"/>
        /// </summary>
        [Key (12)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string ProfileId {get; set;}
        /// <summary>
        /// All ProfileIds of the coop members without the <see cref="Auctioneer"/> because it would be redundant
        /// </summary>
        [Key (13)]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> Coop {set{
            CoopMembers = value?.Select(s=>new UuId(s??AuctioneerId)).ToList();
        }}
        [IgnoreMember]
        public List<UuId> CoopMembers {get;set;}


        [Key (14)]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<object> ClaimedBidders {set{
            ClaimedBids = value?.Select(s=>new UuId((string)s)).ToList();}
        }
        [IgnoreMember]
        public List<UuId> ClaimedBids {get;set;}


        [Key (15)]
        public long HighestBidAmount { get; set; }

        [Key (16)]
        public List<SaveBids> Bids {get; set;}
        [Key (17)]
        public short AnvilUses {get; set;}
        [Key (18)]
        public List<Enchantment> Enchantments {get; set;}

        [Key (19)]
        public NbtData NbtData {get; set;}
        [Key (20)]
        public DateTime ItemCreatedAt{get;set;}
        [Key (21)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        [JsonConverter (typeof (StringEnumConverter))]
        public ItemReferences.Reforge Reforge {get; set;}
        [JsonConverter (typeof (StringEnumConverter))]
        [Key (22)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        public Category Category {get; set;}
        [JsonConverter (typeof (StringEnumConverter))]
        [Key (23)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        public Tier Tier {get; set;}

        public SaveAuction (Hypixel.NET.SkyblockApi.Auction auction) {
            ClaimedBids = auction.ClaimedBidders.Select(s=>new UuId((string)s)).ToList();
            Claimed = auction.Claimed;
            //ItemBytes = auction.ItemBytes;
            StartingBid = auction.StartingBid;
            if (Enum.TryParse (auction.Tier, true, out Tier tier))
                Tier = tier;
            else
                OldTier = auction.Tier;
            if (Enum.TryParse (auction.Category, true,out Category category))
                Category = category;
            else
                OldCategory = auction.Category;
            // make sure that the lenght is shorter than max
            ItemName = auction.ItemName;
            End = auction.End;
            Start = auction.Start;
            Coop = auction.Coop;

            ProfileId = auction.ProfileId == auction.Auctioneer ? null : auction.ProfileId;
            AuctioneerId = auction.Auctioneer;
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
        SPECIAL,
        VERY_SPECIAL,
        MYTHIC

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