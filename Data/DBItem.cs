using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Coflnet.Sky.Core
{
    [DataContract]
    public class DBItem : IItem, IHitCount
    {
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "MEDIUMINT(9)")]
        [IgnoreDataMember]
        public int Id { get; set; }

        private string _tag;

        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        [DataMember(Name = "tag")]
        public string Tag
        {
            get
            {
                return _tag;
            }
            set
            {
                _tag = value.Truncate(44);
            }
        }

        [MySqlCharSet("utf8")]
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "altNames")]
        public List<AlternativeName> Names { get; set; }

        [MySqlCharSet("utf8")]
        [DataMember(Name = "description")]
        public string Description { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [DataMember(Name = "category")]
        public Category Category { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [DataMember(Name = "tier")]

        public Tier Tier { get; set; }
        [DataMember(Name = "iconUrl")]

        public string IconUrl { get; set; }

        [MySqlCharSet("utf8")]
        [DataMember(Name = "extra")]
        public string Extra { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        [MySqlCharSet("utf8")]
        [DataMember(Name = "minecraftType")]
        public string MinecraftType { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(12)]
        [DataMember(Name = "color")]
        public string color { get; set; }

        [IgnoreDataMember]
        public int HitCount { get; set; }
        /// <summary>
        /// true if there has been at least one auction with a reforge for this item
        /// </summary>
        [DataMember(Name = "reforgeable")]
        public bool Reforgeable { get; set; }
        /// <summary>
        /// true if there has been at least one auction with enchantments for this item
        /// </summary>
        [DataMember(Name = "enchantable")]
        public bool Enchantable { get; set; }
        [DataMember(Name = "bazaar")]
        public bool IsBazaar { get; set; }

        public DBItem()
        {
            Names = new List<AlternativeName>();
        }

        public DBItem(ItemDetails.Item item)
        {
            Name = item.Id;
            Extra = item.Extra;
            Tag = item.Id;
            Description = item.Description;

            Enum.TryParse<Category>(item.Category, true, out Category category);
            Category = category;
            Enum.TryParse<Tier>(item.Category, true, out Tier tier);
            Tier = tier;
            IconUrl = item.IconUrl;
            color = item.color;
            MinecraftType = item.MinecraftType?.Length > 44 ? item.MinecraftType.Substring(0, 44) : item.MinecraftType;
            Names = new List<AlternativeName>(item.AltNames.Select(n => new AlternativeName() { Name = n }));
        }
    }

    public interface IItem
    {
        string MinecraftType { get; set; }
        string IconUrl { get; set; }
    }
}