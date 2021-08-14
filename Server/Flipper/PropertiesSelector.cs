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
            { }
            public Property(string value, int rating)
            {
                Value = value;
                Rating = rating;
            }

        }

        public static IEnumerable<Property> GetProperties(SaveAuction auction)
        {
            var properties = new List<Property>();


            var data = auction.FlatenedNBT;

            if (data.ContainsKey("winning_bid"))
            {
                properties.Add(new Property("Top Bid: " + string.Format("{0:n0}", long.Parse(data["winning_bid"])), 20));
            }
            if (data.ContainsKey("hpc"))
                properties.Add(new Property("HPB: " + data["hpc"], 12));
            if (data.ContainsKey("rarity_upgrades"))
                properties.Add(new Property("Recombulated ", 12));
            if (data.ContainsKey("heldItem"))
                properties.Add(new Property($"Holds {ItemDetails.TagToName(data["heldItem"])}", 12));
            if (data.ContainsKey("candyUsed"))
                properties.Add(new Property($"Candy Used {data["candyUsed"]}", 11));
            if (data.ContainsKey("farming_for_dummies_count"))
                properties.Add(new Property($"Farming for dummies {data["farming_for_dummies_count"]}", 11));

                
            var isBook = auction.Tag == "ENCHANTED_BOOK";

            properties.AddRange(auction.Enchantments.Where(e => isBook || FlipperEngine.UltimateEnchants.ContainsKey(e.Type) || e.Level > 5).Select(e => new Property()
            {
                Value = $"{ItemDetails.TagToName(e.Type.ToString())}: {e.Level}",
                Rating = 2 + e.Level + (FlipperEngine.UltimateEnchants.ContainsKey(e.Type) ? 5 : 0)
            }));

            return properties;
        }
    }
}