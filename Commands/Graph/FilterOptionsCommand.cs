using MessagePack;
using Newtonsoft.Json;

namespace hypixel
{
    public abstract class FilterOptionsCommand : Command
    {
        [MessagePackObject]
        public class Formatted
        {
            [Key("label")]
            [JsonProperty("label")]
            public string Label { get; set; }
            [Key("id")]
            [JsonProperty("id")]
            public int Id { get; set; }

            public Formatted(string label, int id)
            {
                Label = label;
                Id = id;
            }
        }
    }
}


