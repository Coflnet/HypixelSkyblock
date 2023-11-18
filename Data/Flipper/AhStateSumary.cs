using System;
using System.Collections.Concurrent;
using System.Runtime.Serialization;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Sumary of the auction house state
    /// </summary>
    [DataContract]
    public class AhStateSumary
    {
        [DataMember(Name = "active")]
        public ConcurrentDictionary<long,long> ActiveAuctions = new ConcurrentDictionary<long, long>();
        [DataMember(Name = "itemCount")]
        public ConcurrentDictionary<string,short> ItemCount = new ConcurrentDictionary<string, short>();
        [DataMember(Name = "time")]
        public DateTime Time;
        [DataMember(Name = "part")]
        public int Part;
        [DataMember(Name = "partCount")]
        public int PartCount;

        public AhStateSumary Clone()
        {
            return new AhStateSumary()
            {
                ActiveAuctions = new ConcurrentDictionary<long, long>(ActiveAuctions),
                ItemCount = new ConcurrentDictionary<string, short>(ItemCount),
                Time = Time,
                Part = Part,
                PartCount = PartCount
            };
        }
    }
}