using System.Collections.Generic;
using MessagePack;

namespace hypixel
{
    public partial class ItemDetails
    {
        [MessagePackObject]
        public class Item
        {
            public static Item Default = new Item(){Id = "unknown",Description="This item has not yet been reviewed by our team"};


            [Key(0)]
            public string Id;
            [Key(1)]
            public HashSet<string> AltNames = new HashSet<string>();
            [Key(2)]
            public string Description;
            [Key(3)]
            public string IconUrl;
            [Key(4)]
            public string Category;
            [Key(5)]
            public string Extra;
            [Key(6)]
            public string Tier;
            [Key(7)]
            public string MinecraftType;
            [Key(8)]
            public string color;
        }
    }
}
