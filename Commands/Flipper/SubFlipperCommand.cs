using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            try
            {

                if (!data.User.HasPremium)
                    Flipper.FlipperEngine.Instance.AddNonConnection(con, (int)data.mId);
                else
                    Flipper.FlipperEngine.Instance.AddConnection(con, (int)data.mId);
            }
            catch (CoflnetException)
            {
                Flipper.FlipperEngine.Instance.AddNonConnection(con, (int)data.mId);
            }
            data.Ok();
        }
    }

    public class BasedOnCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            System.Console.WriteLine(uuid);
            using (var context = new HypixelContext())
            {
                var auction = context.Auctions
                    .Where(a => a.Uuid == uuid)
                    .Include(a => a.NbtData)
                    .Include(a => a.Enchantments)
                    .FirstOrDefault();
                if (auction == null)
                    throw new CoflnetException("auction_unkown", "not found");
                var result = Flipper.FlipperEngine.Instance.GetRelevantAuctions(auction, context);
                result.Wait();
                data.SendBack(data.Create("basedOnResp", result.Result.Item1
                            .Select(a => new { 
                                uuid = a.Uuid, 
                                highestBid = a.HighestBidAmount, 
                                end = a.End }), 
                            A_HOUR));
            }
        }
    }
}