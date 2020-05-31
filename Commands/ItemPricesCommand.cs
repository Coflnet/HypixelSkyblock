using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class ItemPricesCommand : Command
    {
        public class DayCache 
        {
            public List<Result> Results {get;set;}
        }

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


            var fromCache = GetFromCache(details.name,details.Start,details.End);

            // determine the last
            DateTime dbQueryStart = details.Start;
            if(fromCache.Count > 0)
            {
                dbQueryStart = fromCache.Last().End;
            }
            Console.WriteLine($"Found {fromCache.Count} in cache");

            var fromDB = QueryDBFor(details.name,dbQueryStart,details.End);


            var res = new List<Result>();
            res.AddRange(fromCache);
            res.AddRange(fromDB);

            data.SendBack (MessageData.Create("itemResponse",res));


            // cache db
            Cache(details.name, fromDB);

     

        }

        private IEnumerable<Result> QueryDBFor(string itemName, DateTime start, DateTime end)
        {
            using(var context = new HypixelContext())
            {
                //Console.WriteLine(TagID);  item.End.Ticks / time.Ticks *time.Ticks 
                return context.Auctions.Where(auction=>auction.ItemName == itemName)
                            .Where(auction=>auction.End > start && auction.End < end)
                            .Where(auction=>auction.HighestBidAmount>1)
                            .GroupBy(item=> new {item.End.Year, item.End.Month,item.End.Day,item.End.Hour} )
                            .Select(item=>
                            new {
                                End = new DateTime(item.Key.Year,item.Key.Month,item.Key.Day,item.Key.Hour,0,0),
                                Price =  (int)item.Average(a=>((int)a.HighestBidAmount)/a.Count),///(a.Count == 0 ? 1 : a.Count)),
                                Count =   item.Sum(a=> a.Count)
                                //Bids =  (long) item.Sum(a=> a.Bids.Count)
                            }).ToList().Select(i=>new Result(){Count=i.Count,End=i.End,Price=i.Price});

                // cache result
                
            }
        }

        private static Dictionary<string,Dictionary<int,DayCache>> cache = new Dictionary<string, Dictionary<int, DayCache>>();

        public static int CacheSize => cache.Sum(c=>c.Value.Count);

        DateTime earliest = new DateTime(2019,10,10);

        private void Cache(string name, IEnumerable<Result> result)
        {
            var byDay = result.GroupBy(r=>r.End.Date).OrderByDescending(r=>r.Key);

            Console.WriteLine($"Caching total of {byDay.Count()}");

            // today 
            if(byDay.Count() < 2)
                return;
            
            if(!cache.TryGetValue(name,out Dictionary<int,DayCache> dayCache))
            {
                dayCache = new Dictionary<int, DayCache>();
                cache[name] = dayCache;
            }
            foreach (var day in byDay)
            {
                dayCache[GetKey(day.Key)] = new DayCache(){Results=day.ToList()};
            }
        }

        private List<Result> GetFromCache(string itemName, DateTime start, DateTime end)
        {
            SetBoundaries(ref start, ref end);
            List<Result> result = new List<Result>();

            if (!cache.ContainsKey(itemName))
                return result;

            var cacheForItem = cache[itemName];

            Console.WriteLine($"{start} {end} {itemName} ");

            // find cacheItems
            for (var index = start; index < end; index = index.AddDays(1))
            {
                var key = GetKey(index);
                if (cacheForItem.ContainsKey(key))
                    result.AddRange(cacheForItem[key].Results);
            }

            return result;
        }

        private void SetBoundaries(ref DateTime start, ref DateTime end)
        {
            if (start > end)
            {
                start = end;
            }
            if (end > DateTime.Now.Add(new TimeSpan(7, 0, 0, 0)))
            {
                end = DateTime.Now;
            }
            if (start < earliest)
            {
                start = earliest;
            }
        }

        private int GetKey(DateTime index)
        {
            return (int)(index - earliest).TotalDays;
        }

        [MessagePackObject]
        public class Result
        {
            [Key("end")]
            public DateTime End;
            [Key("price")]
            public int Price;
            [Key("count")]
            public int Count;

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
