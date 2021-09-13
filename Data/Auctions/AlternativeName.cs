using MessagePack;
using Newtonsoft.Json;

namespace hypixel
{
    [MessagePackObject(true)]
    public class AlternativeName
    {
        [JsonIgnore]
        [IgnoreMember]
        public int Id { get; set; }

        [MySql.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonIgnore]
        [IgnoreMember]
        public int DBItemId { get; set; }
        [JsonIgnore]
        [IgnoreMember]
        public int OccuredTimes { get; set; }

        public static implicit operator string(AlternativeName name) => name.Name;

    }

}