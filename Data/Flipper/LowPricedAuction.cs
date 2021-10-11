using System.Collections.Generic;
using System.Runtime.Serialization;
using hypixel;

namespace Coflnet.Sky
{
    [DataContract]
    public class LowPricedAuction
    {
        [DataMember(Name = "target")]
        public int TargetPrice;
        [DataMember(Name = "vol")]
        public float DailyVolume;
        [DataMember(Name = "auc")]
        public SaveAuction Auction;
        [DataMember(Name = "finder")]
        public FinderType Finder;

        [IgnoreDataMember]
        public long UId => AuctionService.Instance.GetId(this.Auction.Uuid);

        public enum FinderType
        {
            UNKOWN,
            FLIPPER,
            SNIPER
        }
    }
}