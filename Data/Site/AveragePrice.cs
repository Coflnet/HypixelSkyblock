using System;
using System.Runtime.Serialization;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [DataContract]
    [MessagePackObject]
    public class AveragePrice
    {
        [IgnoreDataMember]
        public int Id {get;set;}
        [DataMember(Name = "min")]
        [Key("min")]
        public float Min { get; set; }
        [DataMember(Name = "max")]
        [Key("max")]
        public float Max { get; set; }
        [DataMember(Name = "avg")]
        [Key("avg")]
        public double Avg { get; set; }
        [DataMember(Name = "volume")]
        [Key("volume")]
        public int Volume { get; set; }
        [IgnoreDataMember]
        public virtual int ItemId { get; set; }
        [DataMember(Name = "time")]
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