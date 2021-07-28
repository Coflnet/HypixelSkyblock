using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using dev;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace hypixel
{
    public class PriceSumaryCommand : Command
    {
        public override async Task Execute(MessageData data)
        {

            using (var context = new HypixelContext())
            {
                var id = ItemDetails.Instance.GetItemIdForName(data.GetAs<string>());
                var minTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                var auctions = (await context.Auctions.Where(a => a.ItemId == id && a.End < DateTime.Now && a.End > minTime && a.HighestBidAmount > 0)
                                .Select(a => a.HighestBidAmount).ToListAsync()).OrderByDescending(p => p).ToList();
                var mode = auctions.GroupBy(a => a).OrderByDescending(a => a.Count()).FirstOrDefault();
                var result = new Result()
                {
                    Max = auctions.FirstOrDefault(),
                    Med = auctions.Count > 0 ? auctions.Skip(auctions.Count() / 2).FirstOrDefault() : 0,
                    Min = auctions.LastOrDefault(),
                    Mean = auctions.Average(),
                    Mode = mode.Key,
                    Volume = auctions.Count()
                };

                await data.SendBack(data.Create("pricesum", result,A_DAY));
            }
        }

        [DataContract]
        public class Result
        {
            [DataMember(Name = "max")]
            public long Max { get; set; }
            [DataMember(Name = "min")]
            public long Min;
            [DataMember(Name = "median")]
            public long Med;
            [DataMember(Name = "mean")]
            public double Mean;
            [DataMember(Name = "mode")]
            public long Mode;
            [DataMember(Name = "volume")]
            public long Volume;
        }
    }
}
