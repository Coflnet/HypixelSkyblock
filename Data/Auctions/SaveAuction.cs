using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Item in sql database
    /// </summary>
    public interface IDbItem
    {
        string ItemName { get; set; }
        string Tag { get; set; }
        long Count { get; set; }
        IEnumerable<NBTLookup> NBTLookup { get; set; }
    }
    [MessagePackObject]
    public class SaveAuction : IDbItem
    {
        [IgnoreMember]
        [JsonIgnore]
        public int Id { get; set; }
        [Key(0)]
        //[JsonIgnore]
        [JsonProperty("uuid")]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string Uuid { get; set; }

        [Key(1)]
        [JsonIgnore]
        public bool Claimed { get; set; }

        [Key(2)]
        [JsonProperty("count")]
        public int Count { get; set; }

        [Key(3)]
        [JsonProperty("startingBid")]
        public long StartingBid { get; set; }

        [Key(4)]
        [JsonIgnore]
        public string OldTier
        {
            set
            {
                if (value == null)
                    return;
                Enum.TryParse<Tier>(value, true, out var tier);
                Tier = tier;
            }
        }


        [Key(5)]
        [JsonIgnore]
        public string OldCategory
        {
            set
            {
                if (value == null)
                    return;
                Category = (Category)Enum.Parse(typeof(Category), value, true);
            }
        }

        private string _tag;
        [Key(6)]
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        [JsonProperty("tag")]
        public string Tag
        {
            get
            {
                return _tag;
            }
            set
            {
                _tag = value.Truncate(40);
            }
        }
        // [Key (7)]
        //public string ItemLore;

        private string _itemName;
        [Key(8)]
        [System.ComponentModel.DataAnnotations.MaxLength(45)]
        [MySqlCharSet("utf8")]
        [JsonProperty("itemName")]
        public string ItemName { get { return _itemName; } set { _itemName = value?.Substring(0, Math.Min(value.Length, 45)); } }

        [Key(9)]
        [JsonProperty("start")]
        public DateTime Start { get; set; }

        [Key(10)]
        [JsonProperty("end")]
        public DateTime End { get; set; }

        [Key(11)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        [JsonProperty("auctioneerId")]
        public string AuctioneerId { get; set; }
        /*    [IgnoreMember]
            [System.ComponentModel.DataAnnotations.Schema.ForeignKey("AuctioneerId")]
            public Player Auctioneer {get;set;} */

        /// <summary>
        /// null if it is the same as the <see cref="AuctioneerId"/>
        /// </summary>
        [Key(12)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        [JsonProperty("profileId")]
        public string ProfileId { get; set; }
        /// <summary>
        /// All ProfileIds of the coop members without the <see cref="AuctioneerId"/> because it would be redundant
        /// </summary>
        [Key(13)]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> Coop
        {
            set
            {
                CoopMembers = value?.Select(s => new UuId(s ?? AuctioneerId)).ToList();
            }
            get
            {
                return CoopMembers?.Select(s => s.value).ToList();
            }
        }
        [IgnoreMember]
        public List<UuId> CoopMembers { get; set; }


        [Key(14)]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [JsonIgnore]
        public List<object> ClaimedBidders
        {
            set
            {
                ClaimedBids = value?.Select(s => new UuId((string)s)).ToList();
            }
        }
        [IgnoreMember]
        [JsonIgnore]
        public List<UuId> ClaimedBids { get; set; }


        [Key(15)]
        [JsonProperty("highestBidAmount")]
        public long HighestBidAmount { get; set; }

        [Key(16)]
        [JsonProperty("bids")]
        public List<SaveBids> Bids { get; set; }
        [Key(17)]
        [JsonProperty("anvilUses")]
        public short AnvilUses { get; set; }
        [Key(18)]
        [JsonProperty("enchantments")]
        public List<Enchantment> Enchantments { get; set; }

        [Key(19)]
        [JsonProperty("nbtData")]
        public NbtData NbtData { get; set; }
        [IgnoreMember]
        [JsonIgnore]
        public int? NbtDataId { get; set; }
        [Key(20)]
        [JsonProperty("itemCreatedAt")]
        public DateTime ItemCreatedAt { get; set; }
        [Key(21)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("reforge")]
        public ItemReferences.Reforge Reforge { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("category")]
        [Key(22)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        public Category Category { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [Key(23)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        [JsonProperty("tier")]
        public Tier Tier { get; set; }

        [Key(24)]
        [JsonProperty("bin")]
        public bool Bin { get; set; }
        [Key(25)]
        [JsonIgnore]
        public int SellerId { get; set; }
        [IgnoreMember]
        [JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "MEDIUMINT(9)")]
        public int ItemId { get; set; }
        [IgnoreMember]
        [JsonIgnore]
        public IEnumerable<NBTLookup> NBTLookup { get; set; }

        private Dictionary<string, string> _flatenedNBT;
        [Key(30)]
        [JsonProperty("flatNbt")]
        [System.Text.Json.Serialization.JsonPropertyName("flatNbt")]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public virtual Dictionary<string, string> FlatenedNBT
        {
            get
            {
                if (_flatenedNBT != null)
                    return _flatenedNBT;
                try
                {
                    var data = NbtData?.Data;
                    if (data == null || data.Count == 0)
                        return new();
                    var preFlattened = NBT.FlattenNbtData(data);
                    SetFlattenedNbt(preFlattened);
                    return _flatenedNBT;
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "getting flat NBT on " + Uuid + " failed");
                    return null;
                }
            }
            set
            {
                _flatenedNBT = value;
            }
        }

        public void SetFlattenedNbt(List<KeyValuePair<string, object>> preFlattened)
        {
            _flatenedNBT = preFlattened.GroupBy(p=>p.Key).Select(p=>p.First()).ToDictionary(d => d.Key, d =>
            {
                if (d.Value is List<object> list)
                    return string.Join(",", list);
                if (d.Value is byte[] array)
                    return Convert.ToBase64String(array);
                return d.Value.ToString();
            });
        }

        /// <summary>
        /// The first part of a uuid converted to a long for faster lookups
        /// </summary>
        [Key(26)]
        [JsonIgnore]
        public long UId { get; set; }
        [Key(27)]
        [JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime FindTime { get; set; } = DateTime.UtcNow;
        //[Key(28)]
        //[JsonIgnore]
        //[System.ComponentModel.DataAnnotations.Schema.NotMapped]
        //public TextMap TraceContext { get; set; }

        /// <summary>
        /// General context for this auction (additional fields)
        /// </summary>
        /// <value></value>
        [Key(29)]
        [JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Dictionary<string, string> Context { get; set; }
        long IDbItem.Count { get => Count; set => Count = (int)value; }

        public SaveAuction() { }

        public SaveAuction(SaveAuction auction)
        {
            if (auction == null)
                return;
            Id = auction.Id;
            Uuid = auction.Uuid;
            Claimed = auction.Claimed;
            Count = auction.Count;
            StartingBid = auction.StartingBid;
            Tag = auction.Tag;
            ItemName = auction.ItemName;
            Start = auction.Start;
            End = auction.End;
            AuctioneerId = auction.AuctioneerId;
            ProfileId = auction.ProfileId;
            CoopMembers = auction.CoopMembers;
            ClaimedBids = auction.ClaimedBids;
            HighestBidAmount = auction.HighestBidAmount;
            Bids = auction.Bids;
            AnvilUses = auction.AnvilUses;
            Enchantments = auction.Enchantments;
            NbtData = auction.NbtData;
            ItemCreatedAt = auction.ItemCreatedAt;
            Reforge = auction.Reforge;
            Category = auction.Category;
            Tier = auction.Tier;
            Bin = auction.Bin;
            SellerId = auction.SellerId;
            ItemId = auction.ItemId;
            NBTLookup = auction.NBTLookup;
            UId = auction.UId;
            FindTime = auction.FindTime;
            Context = Context;
        }

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