using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace hypixel
{
    class AuctionDetails : Command
    {
        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-f0-9]");
            var search = rgx.Replace(data.Data, "");
            var auction = StorageManager.GetOrCreateAuction(search);
            if(auction == null || auction.ItemName == "not_found")
            {
                throw new CoflnetException("error",$"The Auction `{search}` wasn't found");
            }
            var result = JsonConvert.SerializeObject(auction);
            data.SendBack(new MessageData("auctionDetailsResponse",result));
        }
    }
}
