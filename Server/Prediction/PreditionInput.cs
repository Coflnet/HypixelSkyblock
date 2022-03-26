using System.Collections.Generic;
using System;
using MessagePack;

namespace Coflnet.Sky.Core.Prediction
{
    [MessagePackObject]
    public class PreditionInput
    {
        [Key(0)]
        public int ItemId;
        [Key(1)]
        public DateTime Start;
        [Key(2)]
        public DateTime End;
        [Key(3)]
        public long HighestBid;
        [Key(4)]
        public bool Bin;
        [Key(5)]
        public short AnvilUses;
        [Key(6)]
        public int StartingBid;
        [Key(7)]
        public Category Category;
        [Key(8)]
        public int Rarity;
        [Key(9)]
        public int Reforge;
        [Key(10)]
        public List<(byte, int)> Enchantments;
        [Key(11)]
        public List<(short, long)> NbtData;
    }
}