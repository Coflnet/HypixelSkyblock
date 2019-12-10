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
            return other.AuctionCount - AuctionCount;
        }
    }
}