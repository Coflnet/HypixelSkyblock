using System;
using System.Linq;

namespace hypixel
{
    public class AuctionService 
    {
        public static AuctionService Instance;

        static AuctionService()
        {
            Instance = new AuctionService();
        }

        public SaveAuction GetAuction(string uuid, Func<IQueryable<SaveAuction>,IQueryable<SaveAuction>> includeFunc = null)
        {
            var uId = GetId(uuid);
            using(var context = new HypixelContext())
            {
                IQueryable<SaveAuction> select = context.Auctions;
                if(includeFunc != null)
                    select = includeFunc(select);
                var auction = select.Where(a => a.UId == uId).FirstOrDefault();
                if (auction == null)
                {
                    // fall through to old select
                    auction = select.Where(a => a.Uuid == uuid).FirstOrDefault();
                }
                return auction;
            }
        }

        public long GetId(string uuid)
        {
            if (uuid.Length > 17)
                uuid = uuid.Substring(0,17);
            var builder = new System.Text.StringBuilder(uuid);
            builder.Remove(12, 1);
            builder.Remove(16, uuid.Length -17);
            var id = Convert.ToInt64(builder.ToString(), 16);
            if(id == 0)
                id = 1; // allow uId == 0 to be false if not calculated
            return id;
        }

        /// <summary>
        /// This will modify the passed auction and fill in any guessable things such as item name (from tag)
        /// </summary>
        /// <param name="auction"></param>
        /// <returns>The modified original auction</returns>
        public PlayerAuctionsCommand.AuctionResult GuessMissingProperties(PlayerAuctionsCommand.AuctionResult auction)
        {
            if(String.IsNullOrEmpty(auction.ItemName))
                auction.ItemName = ItemDetails.TagToName(auction.Tag);
            if(auction.StartingBid == 0 && auction.Bin)
                auction.StartingBid = auction.HighestBid;

            return auction;
        }
    }
}