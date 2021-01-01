using Newtonsoft.Json;

namespace hypixel
{
    public class AlternativeName
    {
        [JsonIgnore]
        public int Id { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonIgnore]
        public int DBItemId { get; set; }

        public static implicit operator string(AlternativeName name) => name.Name;

    }

}