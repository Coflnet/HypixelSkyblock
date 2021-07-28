

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
            return Ok(new BinResponse(result.FirstOrDefault()?.Price ?? 0));
        }

        public class BinResponse
        {
            public long Lowest;

            public BinResponse(long lowest)
            {
                Lowest = lowest;
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
    }
}

