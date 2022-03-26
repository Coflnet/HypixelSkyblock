using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [DataContract]
    public class ItemSearchQuery
    {
        [DataMember(Name = "name")]
        public string name;

        [DataMember(Name = "count")]
        public int Count;

        [DataMember(Name = "price")]
        public int Price;

        [DataMember(Name = "reforge")]
        public ItemReferences.Reforge Reforge = ItemReferences.Reforge.Any;

        [DataMember(Name = "enchantments")]
        public List<Enchantment> Enchantments;

        [DataMember(Name = "normalized")]
        public bool Normalized;

        [DataMember(Name = "rarity")]
        public Tier Tier;

        [DataMember(Name = "filter")]
        public Dictionary<string, string> Filter;


        [DataMember(Name = "start")]
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

        [DataMember(Name = "end")]
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



    [DataContract]
    public class ActiveItemSearchQuery : ItemSearchQuery
    {
        [DataMember(Name = "order")]
        public SortOrder Order;
        [DataMember(Name = "limit")]
        public int Limit;


        public enum SortOrder
        {
            RELEVANT = 0,
            HIGHEST_PRICE = 1,
            LOWEST_PRICE = 2,
            ENDING_SOON = 4
        }
    }
}
