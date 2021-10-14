using System;
using System.Collections.Concurrent;
using System.Runtime.Serialization;

namespace Coflnet.Sky
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
    }
}