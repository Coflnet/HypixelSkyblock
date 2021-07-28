

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        [Route("item/price/{itemTag}")]
        [HttpGet]
        public async Task<ActionResult<PriceSumaryCommand.Result>> GetSumary(string itemTag)
        {
            var result = await Server.ExecuteCommandWithCache<string, PriceSumaryCommand.Result>("priceSum", itemTag);
            return Ok(result);
        }

        [Route("item/price/{itemTag}/bin")]
        [HttpGet]
        public async Task<ActionResult> GetLowestBin(string itemTag)
        {
            var result = await hypixel.Flipper.FlipperEngine.GetLowestBin(itemTag);
            return Ok(new BinResponse(result.FirstOrDefault()?.Price ?? 0, result.FirstOrDefault()?.Uuid));
        }

        public class BinResponse
        {
            public long Lowest;
            public string Uuid;

            public BinResponse(long lowest, string uuid)
            {
                Lowest = lowest;
                Uuid = uuid;
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
        public async Task<ActionResult> getAuctionDetails(string auctionUuid)
        {
            var result = await Server.ExecuteCommandWithCache<string, SaveAuction>("auctionDetails", auctionUuid);
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

