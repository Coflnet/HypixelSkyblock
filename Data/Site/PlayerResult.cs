using System;
using MessagePack;
using Newtonsoft.Json;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class PlayerResult : IComparable<PlayerResult>
    {
        [Key(0)]
        public string Name;

        [Key(1)]
        [JsonProperty("uuid")]
        public string UUid;

        [Key(2)]
        public int HitCount;

        public PlayerResult(string name, string uUid,int hitCount = 0)
        {
            Name = name;
            UUid = uUid;
            HitCount = hitCount;
        }

        public PlayerResult(){ }

        public int CompareTo(PlayerResult other)
        {
            return (other.HitCount - HitCount) * 1000 + Name.CompareTo(other.Name);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerResult result &&
                   UUid == result.UUid ;
        }

        public override int GetHashCode()
        {
            return UUid.GetHashCode();
        }
    }
}