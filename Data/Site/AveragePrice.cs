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
        public virtual int ItemId { get; set; }
        [Key("time")]
        public DateTime Date { get; set; }

        public override bool Equals(object obj)
        {
            return obj is AveragePrice price &&
                   Volume == price.Volume &&
                   ItemId == price.ItemId &&
                   Date == price.Date;
        }

        public override int GetHashCode()
        {
            int hashCode = 320423494;
            hashCode = hashCode * -1521134295 + Volume.GetHashCode();
            hashCode = hashCode * -1521134295 + ItemId.GetHashCode();
            hashCode = hashCode * -1521134295 + Date.GetHashCode();
            return hashCode;
        }
    }
}