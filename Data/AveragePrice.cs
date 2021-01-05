using System;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class AveragePrice
    {
        [IgnoreMember]
        public int Id {get;set;}
        [Key("min")]
        public float Min { get; set; }
        [Key("max")]
        public float Max { get; set; }
        [Key("avg")]
        public float Avg { get; set; }
        [Key("volume")]
        public int Volume { get; set; }
        [IgnoreMember]
        public int ItemId { get; set; }
        [Key("time")]
        public DateTime Date { get; set; }
    }
}