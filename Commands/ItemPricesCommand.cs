using System;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class ItemPricesCommand : Command
    {
        public override void Execute(MessageData data)
        {
            ItemSearchQuery details;
            try{
                details = data.GetAs<ItemSearchQuery>();
            } catch(Exception e)
            {
                Console.WriteLine(e.Message);
                throw new ValidationException("Format not valid for itemPrices, please see the docs");
            }

            if(details.End == default(DateTime))
            {
                details.End = DateTime.Now;
            }
            Console.WriteLine($"Start: {details.Start} End: {details.End}");

            int hourAmount = 1;
            if(details.End - details.Start > TimeSpan.FromDays(20))
            {
                hourAmount = 6;
            } else if(details.End - details.Start > TimeSpan.FromDays(6))
            {
                hourAmount = 2;
            }
            

            var result = ItemPrices.Instance.Search(details)
                .Where(item=>item.Price > 0 
                        && (!details.Normalized || item.BidCount > 1))
                .GroupBy(item=>ItemPrices.RoundDown(item.End,TimeSpan.FromHours(hourAmount)))
                .Select(item=>
                new Result(){
                    End = item.Key,
                    Price = (long) item.Average(a=>a.Price/(a.Count == 0 ? 1 : a.Count)),
                    Count =  (long) item.Sum(a=> a.Count),
                    Bids =  (long) item.Sum(a=> a.BidCount),
                }).ToList();
            data.SendBack (MessageData.Create("itemResponse",result));
        }

        [MessagePackObject]
        public class Result
        {
            [Key("end")]
            public DateTime End;
            [Key("price")]
            public long Price;
            [Key("count")]
            public long Count;
            [Key("bids")]
            public long Bids;

        }


        [MessagePackObject]
        public class SearchDetails
        {
            [Key("name")]
            public string name;
            [Key("start")]
            public long StartTimeStamp
            {
                set{
                    Start = value.ThisIsNowATimeStamp();
                }
                get {
                    return Start.ToUnix();
                }
            }

            [IgnoreMember]
            public DateTime Start;

            [Key("end")]
            public long EndTimeStamp
            {
                set{
                    if(value == 0)
                    {
                        End = DateTime.Now;
                    } else
                        End = value.ThisIsNowATimeStamp();
                }
                get 
                {
                    return End.ToUnix();
                }
            }

            [IgnoreMember]
            public DateTime End;
        }
    }
}
