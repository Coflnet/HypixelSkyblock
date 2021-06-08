using System.Linq;
using MessagePack;
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
                var batchAmount = 5000;
                var offset = data.GetAs<int>();
                var response = context.Prices.Skip(offset).Take(batchAmount).Select(p=>new AveragePriceSync(p)).ToList();
                if (response.Count == 0)
                    return;
                data.SendBack(new MessageData("pricesSyncResponse", System.Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(response))));

            }
        }

        [MessagePackObject]
        public class AveragePriceSync : AveragePrice
        {
            public AveragePriceSync()
            {
            }
            public AveragePriceSync(AveragePrice price)
            {
                this.Id = price.Id;
                this.ItemId = price.ItemId;
                this.SItemId = price.ItemId;
                this.Max = price.Max;
                this.Avg = price.Avg;
                this.Min = price.Min;
                this.Date = price.Date;
                this.Volume = price.Volume;
            }

            [Key("iId")]
            public int SItemId;

            public override int ItemId { get => SItemId; }
        }
    }
}
