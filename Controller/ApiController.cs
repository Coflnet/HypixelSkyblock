

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// abc
    /// </summary>
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        AuctionService auctionService;
        public ApiController(AuctionService auctionService)
        {
            this.auctionService = auctionService;
        }
        
        /// <summary>
        /// Aggregated sumary of item prices for the last day
        /// </summary>
        /// <param name="itemTag">The item tag you want prices for</param>
        /// <returns></returns>
        [Route("item/price/{itemTag}")]
        [HttpGet]
        public async Task<ActionResult<PriceSumaryCommand.Result>> GetSumary(string itemTag)
        {
            var result = await Server.ExecuteCommandWithCache<string, PriceSumaryCommand.Result>("priceSum", itemTag);
            return Ok(result);
        }

        /// <summary>
        /// Gets the lowest bin by item type
        /// </summary>
        /// <param name="itemTag">The tag of the item to search for bin</param>
        /// <param name="tier">The tier aka rarity of the item. Allows to filter pets and recomulated items</param>
        /// <returns></returns>
        [Route("item/price/{itemTag}/bin")]
        [HttpGet]
        public async Task<ActionResult<BinResponse>> GetLowestBin(string itemTag, [FromQuery] Tier? tier)
        {
            Console.WriteLine(tier);
            var result = await hypixel.Flipper.FlipperEngine.GetLowestBin(itemTag, tier ?? Tier.UNCOMMON);
            return Ok(new BinResponse(result.FirstOrDefault()?.Price ?? 0, result.FirstOrDefault()?.Uuid,result.LastOrDefault()?.Price ?? 0));
        }

        public class BinResponse
        {
            public long Lowest;
            public string Uuid;
            public long SecondLowest;

            public BinResponse(long lowest, string uuid, long secondLowest)
            {
                Lowest = lowest;
                Uuid = uuid;
                SecondLowest = secondLowest;
            }
        }

        /// <summary>
        /// Searches through all items
        /// </summary>
        /// <param name="searchVal">The search term to search for</param>
        /// <returns>An array of search results matching the searchValue</returns>
        [Route("item/search/{searchVal}")]
        [HttpGet]
        public async Task<ActionResult<List<SearchService.SearchResultItem>>> SearchItem(string searchVal)
        {
            var result = await Server.ExecuteCommandWithCache<string, List<SearchService.SearchResultItem>>("itemSearch", searchVal);
            return Ok(result);
        }

        /// <summary>
        /// Full search, includes items, players and enchantments
        /// </summary>
        /// <param name="searchVal">The search term to search for</param>
        /// <returns></returns>
        [Route("search/{searchVal}")]
        [HttpGet]
        public async Task<ActionResult<List<SearchService.SearchResultItem>>> FullSearch(string searchVal)
        {
            var result = await Server.ExecuteCommandWithCache<string, List<SearchService.SearchResultItem>>("fullSearch", searchVal);
            return Ok(result);
        }

        /// <summary>
        /// Retrieve details of a specific auction
        /// </summary>
        /// <param name="auctionUuid">The uuid of the auction you want the details for</param>
        /// <returns></returns>
        [Route("auction/{auctionUuid}")]
        [HttpGet]
        [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<ActionResult<SaveAuction>> getAuctionDetails(string auctionUuid)
        {
            var result = await auctionService.GetAuctionAsync(auctionUuid, auction => auction
                        .Include(a => a.Enchantments)
                        .Include(a => a.NbtData)
                        .Include(a => a.Bids));
            Console.WriteLine("controller hit");
            
            return Ok(result);
        }


        [Route("player/{playerUuid}/bids")]
        [HttpGet]
        public async Task<ActionResult<List<PlayerBidsCommand.BidResult>>> GetPlayerBids(string playerUuid)
        {
            var result = await Server.ExecuteCommandWithCache<PaginatedRequestCommand<PlayerBidsCommand.BidResult>.Request, List<PlayerBidsCommand.BidResult>>(
                "playerBids", new PaginatedRequestCommand<PlayerBidsCommand.BidResult>.Request()
                {
                    Amount = 10,
                    Offset = 0,
                    Uuid = playerUuid
                });
            return Ok(result);
        }

        [Route("player/{playerUuid}/auctions")]
        [HttpGet]
        public async Task<ActionResult<List<PlayerAuctionsCommand.AuctionResult>>> GetPlayerAuctions(string playerUuid)
        {
            var result = await Server.ExecuteCommandWithCache<PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>.Request, List<PlayerAuctionsCommand.AuctionResult>>(
                "playerAuctions", new PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>.Request()
                {
                    Amount = 10,
                    Offset = 0,
                    Uuid = playerUuid
                });
            return Ok(result);
        }
    }
}

