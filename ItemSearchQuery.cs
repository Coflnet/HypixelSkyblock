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
        public ItemReferences.Reforge Reforge = ItemReferences.Reforge.Any;

        [Key("enchantments")]
        public List<Enchantment> Enchantments;

        [Key("normalized")]
        public bool Normalized;

        [Key("rarity")]
        public Tier Tier;

        [Key("filter")]
        public Dictionary<string,string> Filter;


        [Key("start")]
        public long StartTimeStamp
        {
            set
            {
                Start = value.ThisIsNowATimeStamp();
            }
            get
            {
                return Start.ToUnix();
            }
        }

        [IgnoreMember]
        public DateTime Start;

        [Key("end")]
        public long EndTimeStamp
        {
            set
            {
                if (value == 0)
                {
                    End = DateTime.Now;
                }
                else
                    End = value.ThisIsNowATimeStamp();
            }
            get
            {
                return End.ToUnix();
            }
        }

        [IgnoreMember]
        public DateTime End;

        public override bool Equals(object obj)
        {
            return obj is ItemSearchQuery query &&
                   name == query.name &&
                   Count == query.Count &&
                   Price == query.Price &&
                   Reforge == query.Reforge &&
                   EqualityComparer<List<Enchantment>>.Default.Equals(Enchantments, query.Enchantments) &&
                   StartTimeStamp == query.StartTimeStamp &&
                   Start == query.Start &&
                   EndTimeStamp == query.EndTimeStamp &&
                   End == query.End;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(name);
            hash.Add(Count);
            hash.Add(Price);
            hash.Add(Reforge);
            hash.Add(Enchantments);
            hash.Add(StartTimeStamp);
            hash.Add(Start);
            hash.Add(EndTimeStamp);
            hash.Add(End);
            return hash.ToHashCode();
        }

        public enum SortBy
        {
            ASYNC,
            CREATED = 2,
            ENDED = 4
        }
    }
}
