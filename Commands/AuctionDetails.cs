using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;

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
                            .Where (a => a.Uuid == search).ToList ();
                if (result.Count == 0) {
                    throw new CoflnetException ("error", $"The Auction `{search}` wasn't found");
                }
                var resultJson = JsonConvert.SerializeObject (result[0]);
                data.SendBack (new MessageData ("auctionDetailsResponse", resultJson));
            }

        }
    }
}