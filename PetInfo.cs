using Newtonsoft.Json;

namespace hypixel
{

    public class PetInfo
    {
        [JsonProperty("type")]
        public string Type {get;set;}
        [JsonProperty("tier")]
        public string Tier {get;set;}
        [JsonProperty("exp")]
        public long Exp {get;set;}
    }
    

    public class PotionInfo
    {
        [JsonProperty("potion")]
        public string Type {get;set;}
        [JsonProperty("potion_level")]
        public int Level {get;set;}
        [JsonProperty("enhanced")]
        public long Enhanced {get;set;}
    }

}
