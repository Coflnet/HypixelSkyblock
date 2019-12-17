using System;
using System.Collections.Generic;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class ItemSearchQuery
    {
        [Key("name")]
        public string name;

        [Key("count")]
        public int Count;

        [Key("price")]
        public int Price;

        [Key("reforge")]
        public ItemReferences.Reforge Reforge;

        [Key("enchantments")]
        public List<Enchantment> Enchantments;


        [Key("start")]
        public long StartTimeStamp
        {
            set{
                Start = value.ThisIsNowATimeStamp();
            }
            get {
                return Start.ToUnix();
            }
        }

        [IgnoreMember]
        public DateTime Start;

        [Key("end")]
        public long EndTimeStamp
        {
            set{
                if(value == 0)
                {
                    End = DateTime.Now;
                } else
                    End = value.ThisIsNowATimeStamp();
            }
            get 
            {
                return End.ToUnix();
            }
        }

        [IgnoreMember]
        public DateTime End;
    }
}
