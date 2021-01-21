using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hypixel
{
    public class DBItem : IItem, IHitCount
    {
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "MEDIUMINT(9)")]
        [JsonIgnore]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        [JsonProperty("tag")]
        public string Tag { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("altNames")]
        public List<AlternativeName> Names { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("category")]
        public Category Category { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("tier")]

        public Tier Tier { get; set; }
        [JsonProperty("iconUrl")]

        public string IconUrl { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("extra")]
        public string Extra { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("minecraftType")]
        public string MinecraftType { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(12)]
        [JsonProperty("color")]
        public string color { get; set; }

        [JsonIgnore]
        public int HitCount { get; set; }
        /// <summary>
        /// <see cref="true"/> if there has been at least one auction with a reforge for this item
        /// </summary>
        [JsonProperty("reforgeable")]
        public bool Reforgeable { get; set; }
        /// <summary>
        /// <see cref="true"/> if there has been at least one auction with enchantments for this item
        /// </summary>
        [JsonProperty("enchantable")]
        public bool Enchantable { get; set; }
        [JsonProperty("bazaar")]
        public bool IsBazaar { get; set; }

        public DBItem()
        {
            Names = new List<AlternativeName>();
        }

        public DBItem(ItemDetails.Item item)
        {
            this.Name = item.Id;
            this.Extra = item.Extra;
            this.Tag = item.Id;
            this.Description = item.Description;

            Enum.TryParse<Category>(item.Category, true, out Category category);
            this.Category = category;
            Enum.TryParse<Tier>(item.Category, true, out Tier tier);
            this.Tier = tier;
            this.IconUrl = item.IconUrl;
            this.color = item.color;
            this.MinecraftType = item.MinecraftType.Length > 44 ? item.MinecraftType.Substring(0, 44) : item.MinecraftType;
            this.Names = new List<AlternativeName>(item.AltNames.Select(n => new AlternativeName() { Name = n }));
        }
    }

    public interface IItem
    {
        string MinecraftType { get; set; }
        string IconUrl { get; set; }
    }
}