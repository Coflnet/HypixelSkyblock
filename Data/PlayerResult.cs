using System;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class PlayerResult : IComparable<PlayerResult>
    {
        [Key(0)]
        public string Name;

        [Key(1)]
        public string UUid;

        [Key(2)]
        public int AuctionCount;

        public PlayerResult(string name, string uUid)
        {
            Name = name;
            UUid = uUid;
        }

        public PlayerResult(){ }

        public int CompareTo(PlayerResult other)
        {
            return (other.AuctionCount - AuctionCount) * 1000 + Name.CompareTo(other.Name);
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