

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Hypixel.Controller
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        AuctionService auctionService;
        public ApiController(AuctionService auctionService)
        {
            this.auctionService = auctionService;
        }
        
        [Route("item/price/{itemTag}")]
        [HttpGet]
        public async Task<ActionResult<PriceSumaryCommand.Result>> GetSumary(string itemTag)
        {
            var result = await Server.ExecuteCommandWithCache<string, PriceSumaryCommand.Result>("priceSum", itemTag);
            return Ok(result);
        }

        [Route("item/price/{itemTag}/bin")]
        [HttpGet]
        public async Task<ActionResult> GetLowestBin(string itemTag, [FromQuery] Tier? tier)
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


        [Route("item/search/{searchVal}")]
        [HttpGet]
        public async Task<ActionResult> SearchItem(string searchVal)
        {
            var result = await Server.ExecuteCommandWithCache<string, List<SearchService.SearchResultItem>>("itemSearch", searchVal);
            return Ok(result);
        }

        [Route("search/{searchVal}")]
        [HttpGet]
        public async Task<ActionResult<List<SearchService.SearchResultItem>>> FullSearch(string searchVal)
        {
            var result = await Server.ExecuteCommandWithCache<string, List<SearchService.SearchResultItem>>("fullSearch", searchVal);
            return Ok(result);
        }

        [Route("auction/{auctionUuid}")]
        [HttpGet]
        [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<ActionResult> getAuctionDetails(string auctionUuid)
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
        public async Task<ActionResult> GetPlayerBids(string playerUuid)
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
        public async Task<ActionResult> GetPlayerAuctions(string playerUuid)
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

