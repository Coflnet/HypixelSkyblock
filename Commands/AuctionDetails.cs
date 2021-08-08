using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace hypixel
{
    class AuctionDetails : Command
    {
        public override Task Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-f0-9]");
            var search = rgx.Replace(data.Data, "");
            using (var context = new HypixelContext())
            {
                var result = AuctionService.Instance.GetAuction(search, 
                            a => a
                             .Include(a => a.NbtData)
                             .Include(a => a.Enchantments)
                             .Include(a => a.Bids)
                             .Include(a => a.NBTLookup)
                             .Where(a => a.Uuid == search));
                if (result == null)
                {
                    if (Program.LightClient)
                    {
                        return ClientProxy.Instance.Proxy(data);
                    }
                    throw new CoflnetException("error", $"The Auction `{search}` wasn't found");
                }
                var resultJson = JSON.Stringify(result);
                var maxAge = A_MINUTE;
                if (result.End < DateTime.Now)
                    // won't change anymore
                    maxAge = A_WEEK;

                return data.SendBack(new MessageData("auctionDetailsResponse", resultJson, maxAge));
            }

        }
    }
}