using System;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class AuctionReference {
        [Key (0)]
        public string sellerId;
        [Key (1)]
        public string auctionId;

        public AuctionReference (string sellerId, string auctionId) {
            this.sellerId = sellerId;
            this.auctionId = auctionId;
        }

        public AuctionReference () { }

        public override bool Equals (object obj) {
            return obj is AuctionReference reference &&
                sellerId == reference.sellerId &&
                auctionId == reference.auctionId;
        }

        public override int GetHashCode () {
            return HashCode.Combine (sellerId, auctionId);
        }
    }

}