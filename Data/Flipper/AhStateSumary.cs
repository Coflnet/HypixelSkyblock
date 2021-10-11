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
        public ConcurrentDictionary<long,byte> ActiveAuctions;
        [DataMember(Name = "itemCount")]
        public ConcurrentDictionary<string,short> ItemCount;
    }
}