

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
    public class AuctionsController : ControllerBase
    {
        AuctionService auctionService;
        HypixelContext context;

        public AuctionsController(AuctionService auctionService, HypixelContext context)
        {
            this.auctionService = auctionService;
            this.context = context;
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

            return Ok(result);
        }

        /// <summary>
        /// Get the 10 (or how many are available) lowest bins
        /// </summary>
        /// <param name="itemTag">The itemTag to get bins for</param>
        /// <returns></returns>
        [Route("auctions/tag/{itemTag}/active/bin")]
        [HttpGet]
        [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<ActionResult<List<SaveAuction>>> GetLowestBins(string itemTag)
        {
            var itemId = ItemDetails.Instance.GetItemIdForName(itemTag);
            var result = await context.Auctions
                        .Where(a=>a.ItemId == itemId && a.End > DateTime.Now && a.HighestBidAmount == 0 && a.Bin)
                        .Include(a => a.Enchantments)
                        .Include(a => a.NbtData)
                        .OrderBy(a=>a.StartingBid)
                        .Take(10).ToListAsync();

            return Ok(result);
        }
    }
}

