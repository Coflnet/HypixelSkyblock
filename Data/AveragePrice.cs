using System;

namespace hypixel
{
    public class AveragePrice
    {
        public int Id {get;set;}
        public float Min { get; set; }
        public float Max { get; set; }
        public float Avg { get; set; }
        public int Volume { get; set; }
        public int ItemId { get; set; }
        public DateTime Date { get; set; }
    }
}