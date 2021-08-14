using System.Collections.Generic;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Main API endpoints
    /// </summary>
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
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
        /// The last 10 auctions a player bid on
        /// </summary>
        /// <param name="playerUuid">The uuid of the player</param>
        /// <returns></returns>
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

        /// <summary>
        /// The last 10 auctions a player created
        /// </summary>
        /// <param name="playerUuid">The uuid of the player</param>
        /// <returns></returns>
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

