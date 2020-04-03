using Newtonsoft.Json;

namespace hypixel
{
    public partial class ItemDetails
    {
        class PetInfo
        {
            [JsonProperty("type")]
            public string Type {get;set;}
            [JsonProperty("tier")]
            public string Tier {get;set;}
            [JsonProperty("exp")]
            public long Exp {get;set;}
        }
    }
}
