using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Coflnet.Sky.Core
{
    [MessagePackObject(true)]
    public class AlternativeName
    {
        [JsonIgnore]
        [IgnoreMember]
        public int Id { get; set; }

        [MySqlCharSet("utf8")]
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonIgnore]
        [IgnoreMember]
        public int DBItemId { get; set; }
        [JsonIgnore]
        [IgnoreMember]
        public int OccuredTimes { get; set; }

        public static implicit operator string(AlternativeName name) => name?.Name;

    }

}