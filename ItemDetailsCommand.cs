using System;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class ItemDetailsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            SearchDetails details;
            try{
                details = data.GetAs<SearchDetails>();
            } catch(Exception e)
            {
                throw new ValidationException("Format not valid for itemDetails, please see the docs");
            }

            if(details.End == default(DateTime))
            {
                details.End = DateTime.Now;
            }
            Console.WriteLine($"Start: {details.Start} End: {details.End}");

            var result = Program.AuctionsForItem(details.name,details.Start,details.End)
                .Where(item=>item.HighestBidAmount > 0)
                .Select(item=>new Result(){
                    End = item.End,
                    Price = item.HighestBidAmount/item.Count
                }).ToList();
            data.SendBack (MessageData.Create("item",result));
        }

        [MessagePackObject]
        public class Result
        {
            [Key("end")]
            public DateTime End;
            [Key("price")]
            public long Price;
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
