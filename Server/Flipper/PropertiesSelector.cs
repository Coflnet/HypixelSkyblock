using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace hypixel.Flipper
{
    /// <summary>
    /// Takes care of selecting interesting/relevant properties from a flip
    /// </summary>
    public class PropertiesSelector
    {
        [DataContract]
        public class Property
        {
            [DataMember(Name = "val")]
            public string Value;
            /// <summary>
            /// how important is this?
            /// </summary>
            [IgnoreDataMember]
            public int Rating;

            public Property()
            {}
            public Property(string value, int rating)
            {
                Value = value;
                Rating = rating;
            }

        }

        public static IEnumerable<Property> GetProperties(SaveAuction auction)
        {
            var properties = new List<Property>();


            var data = auction.NbtData.Data;

            if(data.ContainsKey("winning_bid"))
            {
                properties.Add(new Property("Top Bid: " + data["winning_bid"], 20));
            }
            if(data.ContainsKey("hpc"))
                properties.Add(new Property("HPB: " + data["hpc"], 12));
            if(data.ContainsKey("rarity_upgrades"))
                properties.Add(new Property("Recombulated ", 12));
            
            properties.AddRange(auction.Enchantments.Where(e => FlipperEngine.UltimateEnchants.ContainsKey(e.Type) || e.Level > 5).Select(e => new Property()
            {
                Value = $"{e.Type.ToString()}: {e.Level}",
                Rating = 2 + e.Level
            }));

            return properties;
        }
    }
}