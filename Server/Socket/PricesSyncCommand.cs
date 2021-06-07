using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebSocketSharp;

namespace hypixel
{
    public class PricesSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var done = false;
                var index = 0;
                var batchAmount = 5000;
                var offset = data.GetAs<int>();
                while (!done)
                {
                    var response = context.Prices.Skip(offset + batchAmount * index++).Take(batchAmount).ToList();
                    if (response.Count == 0)
                        return;
                    data.SendBack(new MessageData("pricesSyncResponse", System.Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(response))));
                }
            }
        }
    }
}
