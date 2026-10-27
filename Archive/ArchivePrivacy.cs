using System.Linq;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>Read-time privacy of archived auctions: opted-out players never leave the archive reads, even before a part was rewritten.</summary>
    public static class ArchivePrivacy
    {
        /// <summary>
        /// Masks opted-out participants of a materialised auction with <see cref="PlayerOptOut.Mask"/> (the rule of the regular auction
        /// endpoints) and clears the Scylla-only player names that belong to a masked identity.
        /// </summary>
        public static void Mask(ArchivedAuction auction)
        {
            var row = auction.Row;
            var extras = auction.Extras;
            var highestBidderOut = PlayerOptOut.IsOptedOut(ArchivedAuctionBlock.HighestBid(row.Bids)?.Bidder);
            var profileOut = PlayerOptOut.IsOptedOut(row.ProfileId) || PlayerOptOut.IsOptedOut(row.AuctioneerId);
            var coopOut = row.CoopMembers?.Any(m => PlayerOptOut.IsOptedOut(m.value)) ?? false;
            PlayerOptOut.Mask(row);
            if (extras == null)
                return;
            if (highestBidderOut)
                extras.HighestBidderName = null;
            if (profileOut)
                extras.ProfileName = null;
            if (coopOut)
                extras.CoopName = null;
        }
    }
}
