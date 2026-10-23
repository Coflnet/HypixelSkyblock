using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StjIgnore = System.Text.Json.Serialization.JsonIgnoreAttribute;
using StjIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition;
using StjName = System.Text.Json.Serialization.JsonPropertyNameAttribute;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Trimmed API shape of an <see cref="ArchivedAuction"/> (same property names with Newtonsoft and System.Text.Json).
    /// Item bytes and bids are only filled when asked for (<see cref="From"/>) and omitted from the JSON when null.
    /// </summary>
    public class ArchivedAuctionDto
    {
        /// <summary>Auction uuid.</summary>
        [JsonProperty("uuid"), StjName("uuid")] public string Uuid { get; set; }
        /// <summary><see cref="SaveAuction.UId"/>.</summary>
        [JsonProperty("uid"), StjName("uid")] public long Uid { get; set; }
        /// <summary>Item tag.</summary>
        [JsonProperty("tag"), StjName("tag")] public string Tag { get; set; }
        /// <summary>Item name.</summary>
        [JsonProperty("itemName"), StjName("itemName")] public string ItemName { get; set; }
        /// <summary><see cref="Core.Tier"/> name.</summary>
        [JsonProperty("tier"), StjName("tier")] public string Tier { get; set; }
        /// <summary><see cref="Core.Category"/> name.</summary>
        [JsonProperty("category"), StjName("category")] public string Category { get; set; }
        /// <summary>BIN auction.</summary>
        [JsonProperty("bin"), StjName("bin")] public bool Bin { get; set; }
        /// <summary>Highest bid above 0.</summary>
        [JsonProperty("sold"), StjName("sold")] public bool Sold { get; set; }
        /// <summary>Start (UTC).</summary>
        [JsonProperty("start"), StjName("start")] public DateTime Start { get; set; }
        /// <summary>End (UTC).</summary>
        [JsonProperty("end"), StjName("end")] public DateTime End { get; set; }
        /// <summary>Starting bid.</summary>
        [JsonProperty("startingBid"), StjName("startingBid")] public long StartingBid { get; set; }
        /// <summary>Highest bid, 0 when unsold.</summary>
        [JsonProperty("highestBidAmount"), StjName("highestBidAmount")] public long HighestBidAmount { get; set; }
        /// <summary>Seller uuid.</summary>
        [JsonProperty("auctioneer"), StjName("auctioneer")] public string Auctioneer { get; set; }
        /// <summary>Seller profile uuid (MariaDB rows: null when it equals the seller).</summary>
        [JsonProperty("profileId"), StjName("profileId")] public string ProfileId { get; set; }
        /// <summary>Uuid of the highest bidder, null without bids.</summary>
        [JsonProperty("highestBidder"), StjName("highestBidder")] public string HighestBidder { get; set; }
        /// <summary>Name of the highest bidder (Scylla rows only).</summary>
        [JsonProperty("highestBidderName"), StjName("highestBidderName")] public string HighestBidderName { get; set; }
        /// <summary>Item count.</summary>
        [JsonProperty("count"), StjName("count")] public int Count { get; set; }
        /// <summary>Enchantment name to level (archived types, plus Scylla's unknown ones).</summary>
        [JsonProperty("enchantments"), StjName("enchantments")] public Dictionary<string, int> Enchantments { get; set; }
        /// <summary>String-keyed flattened nbt (<see cref="ArchivedAuction.NbtStrings"/>).</summary>
        [JsonProperty("nbtLookup"), StjName("nbtLookup")] public Dictionary<string, string> NbtLookup { get; set; }
        /// <summary>Coop member uuids.</summary>
        [JsonProperty("coop"), StjName("coop")] public List<string> Coop { get; set; }
        /// <summary>Number of bids.</summary>
        [JsonProperty("bidsCount"), StjName("bidsCount")] public int BidsCount { get; set; }
        /// <summary>Source of the block.</summary>
        [JsonProperty("source"), StjName("source")] public string Source { get; set; }
        /// <summary>Part of the tag-month.</summary>
        [JsonProperty("part"), StjName("part")] public int Part { get; set; }
        /// <summary>Bids (same shape as <see cref="SaveAuction.Bids"/>), only when requested.</summary>
        [JsonProperty("bids", NullValueHandling = NullValueHandling.Ignore), StjName("bids"), StjIgnore(Condition = StjIgnoreCondition.WhenWritingNull)]
        public List<SaveBids> Bids { get; set; }
        /// <summary>Base64 of the raw item bytes, only when requested.</summary>
        [JsonProperty("itemBytes", NullValueHandling = NullValueHandling.Ignore), StjName("itemBytes"), StjIgnore(Condition = StjIgnoreCondition.WhenWritingNull)]
        public string ItemBytes { get; set; }

        /// <summary>Map an archived auction.</summary>
        public static ArchivedAuctionDto From(ArchivedAuction auction, bool includeItemBytes = false, bool includeBids = false)
        {
            if (auction == null)
                return null;
            var r = auction.Row;
            var enchantments = new Dictionary<string, int>();
            foreach (var kv in ArchiveExport.Enchantments(auction))
                enchantments[kv.Key] = kv.Value;
            return new ArchivedAuctionDto
            {
                Uuid = r.Uuid,
                Uid = r.UId,
                Tag = r.Tag,
                ItemName = r.ItemName,
                Tier = r.Tier.ToString(),
                Category = r.Category.ToString(),
                Bin = r.Bin,
                Sold = auction.Sold,
                Start = r.Start,
                End = r.End,
                StartingBid = r.StartingBid,
                HighestBidAmount = r.HighestBidAmount,
                Auctioneer = r.AuctioneerId,
                ProfileId = r.ProfileId,
                HighestBidder = auction.HighestBidder,
                HighestBidderName = auction.Extras?.HighestBidderName,
                Count = r.Count,
                Enchantments = enchantments,
                NbtLookup = auction.NbtStrings.ToDictionary(kv => kv.Key, kv => kv.Value),
                Coop = r.CoopMembers?.Select(c => c.value).ToList() ?? new List<string>(),
                BidsCount = r.Bids?.Count ?? 0,
                Source = auction.Source,
                Part = auction.Part,
                Bids = includeBids ? r.Bids ?? new List<SaveBids>() : null,
                ItemBytes = includeItemBytes && r.NbtData?.data != null ? Convert.ToBase64String(r.NbtData.data) : null,
            };
        }
    }
}
