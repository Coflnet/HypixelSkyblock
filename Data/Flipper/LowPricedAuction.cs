using System.Collections.Generic;
using System.Runtime.Serialization;
using System;

namespace Coflnet.Sky.Core
{
    [DataContract]
    public class LowPricedAuction
    {
        [DataMember(Name = "target")]
        public long TargetPrice;
        [DataMember(Name = "vol")]
        public float DailyVolume;
        [DataMember(Name = "auc")]
        public SaveAuction Auction;
        [DataMember(Name = "finder")]
        public FinderType Finder;
        [DataMember(Name = "props")]
        public Dictionary<string, string> AdditionalProps = new();
        [IgnoreDataMember]
        public long UId => AuctionService.Instance.GetId(Auction.Uuid);

        // copy constructor
        public LowPricedAuction(LowPricedAuction other)
        {
            TargetPrice = other.TargetPrice;
            DailyVolume = other.DailyVolume;
            Auction = new SaveAuction(other.Auction);
            Finder = other.Finder;
            AdditionalProps = other.AdditionalProps;
        }

        public LowPricedAuction()
        {
        }

        [Flags]
        public enum FinderType
        {
            UNKOWN,
            FLIPPER = 1,
            SNIPER = 2,
            SNIPER_MEDIAN = 4,
            AI = 8,
            SNIPERS = SNIPER_MEDIAN | SNIPER,
            FLIPPER_AND_SNIPERS = FLIPPER | SNIPERS,
            USER = 16,

            TFM = 32,
            STONKS = 64,
            EXTERNAL = 128,
            BINMASTER = 256,
            LEIKO = 512,
            CraftCost = 1024,
            MedianBased = SNIPER_MEDIAN | FLIPPER,
            ALL_EXCEPT_USER = FLIPPER_AND_SNIPERS | AI | TFM | STONKS | EXTERNAL | BINMASTER,
        }
    }
}