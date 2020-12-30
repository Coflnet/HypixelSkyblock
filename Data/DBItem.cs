using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel
{
    public class DBItem  : IItem,IHitCount
    {
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "MEDIUMINT(9)")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        public string Tag { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        public string Name { get; set; }

        public List<AlternativeName> Names { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        public string Description { get; set; }
        public Category Category { get; set; }
        public Tier Tier { get; set; }
        public string IconUrl { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        public string Extra { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(44)]
        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        public string MinecraftType { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(12)]
        public string color { get; set; }

        public int HitCount {get;set;}

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
        string MinecraftType {get;set;}
        string IconUrl {get;set;}
    }
}