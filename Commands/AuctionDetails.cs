using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System;

namespace hypixel {
    class AuctionDetails : Command {
        public override void Execute (MessageData data) {
            Regex rgx = new Regex ("[^a-f0-9]");
            var search = rgx.Replace (data.Data, "");
            using (var context = new HypixelContext ()) {
                var result = context.Auctions
                            .Include(a=>a.NbtData)
                            .Include(a=>a.Enchantments)
                            .Include(a=>a.Bids)
                            .Where (a => a.Uuid == search).FirstOrDefault ();
                if (result == null) {
                    throw new CoflnetException ("error", $"The Auction `{search}` wasn't found");
                }
                var resultJson = JSON.Stringify (result);
                var maxAge = A_MINUTE;
                if(result.End > DateTime.Now)
                    // won't change anymore
                    maxAge = A_WEEK;

                data.SendBack (new MessageData ("auctionDetailsResponse", resultJson,maxAge));
            }

        }
    }
}