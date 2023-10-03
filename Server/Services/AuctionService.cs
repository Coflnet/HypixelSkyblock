using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Core
{
    public class AuctionService
    {
        public static AuctionService Instance;

        static AuctionService()
        {
            Instance = new AuctionService();
        }

        public SaveAuction GetAuction(string uuid, Func<IQueryable<SaveAuction>, IQueryable<SaveAuction>> includeFunc = null)
        {
            return GetAuctionAsync(uuid, includeFunc).Result;
        }
        public async Task<SaveAuction> GetAuctionAsync(string uuid, Func<IQueryable<SaveAuction>, IQueryable<SaveAuction>> includeFunc = null)
        {
            var uId = GetId(uuid);
            using (var context = new HypixelContext())
            {
                IQueryable<SaveAuction> select = context.Auctions;
                if (includeFunc != null)
                    select = includeFunc(select);
                var auction = await select.Where(a => a.UId == uId).FirstOrDefaultAsync();
                return auction;
            }
        }

        public long GetId(string uuid)
        {
            if (uuid == null)
                return -1;
            if (uuid.Length > 17)
                uuid = uuid.Replace("-", "").Substring(0, 17);
            var builder = new System.Text.StringBuilder(uuid);
            builder.Remove(12, 1);
            if (uuid.Length > 16)
                builder.Remove(16, uuid.Length - 17);
            var id = Convert.ToInt64(builder.ToString(), 16);
            if (id == 0)
                id = 1; // allow uId == 0 to be false if not calculated
            return id;
        }

        /// <summary>
        /// Reverse of <see cref="GetId(string)"/>
        /// </summary>
        /// <param name="internId"></param>
        /// <returns></returns>
        public string GetUuid(long internId)
        {
            var builder = new System.Text.StringBuilder(internId.ToString("x"));
            if (internId < 5_000_000_000 && internId >= 0)
                return builder.ToString();
            return builder.Insert(12, 4).ToString();
        }

        /// <summary>
        /// This will modify the passed auction and fill in any guessable things such as item name (from tag)
        /// </summary>
        /// <param name="auction"></param>
        /// <returns>The modified original auction</returns>
        public AuctionResult GuessMissingProperties(AuctionResult auction)
        {
            if (String.IsNullOrEmpty(auction.ItemName))
                auction.ItemName = ItemDetails.TagToName(auction.Tag);
            if (auction.StartingBid == 0 && auction.Bin)
                auction.StartingBid = auction.HighestBid;

            return auction;
        }

        public T GetAuctionWithSelect<T>(string uuid, Func<IQueryable<SaveAuction>, T> selectFunc)
        {
            var uId = GetId(uuid);
            using (var context = new HypixelContext())
            {
                IQueryable<SaveAuction> select = context.Auctions.Where(a => a.UId == uId);
                return selectFunc(select);
            }
        }
    }
}