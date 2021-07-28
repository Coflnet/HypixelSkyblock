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
                var id = await context.Items.Where(i => i.Tag == data.GetAs<string>()).Select(i => i.Id).FirstOrDefaultAsync();
                var minTime = DateTime.Now.Subtract(TimeSpan.FromDays(3));
                var auctions = await context.Auctions.Where(a => a.ItemId == id && a.End < DateTime.Now && a.Start > minTime && a.HighestBidAmount > 0)
                                .Select(a => a.HighestBidAmount).OrderByDescending(p => p).ToListAsync();

                var result = new Result()
                {
                    Max = auctions.FirstOrDefault(),
                    Med = auctions.Count > 0 ? auctions.Skip(auctions.Count() / 2).FirstOrDefault() : 0,
                    Min = auctions.LastOrDefault(),
                    Volume = auctions.Count()
                };
                if(result.Volume == 0)
                    throw new CoflnetException("aaa", "bb");

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
            [DataMember(Name = "volume")]
            public long Volume;
        }
    }
}
