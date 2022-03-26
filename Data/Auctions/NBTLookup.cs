using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class NBTLookup
    {
        [Key(0)]
        public int AuctionId { get; set; }
        [Key(1)]
        public short KeyId { get; set; }
        [Key(2)]
        public long Value { get; set; }

        public NBTLookup(short keyId, long value)
        {
            KeyId = keyId;
            Value = value;
        }

        public NBTLookup()
        {
        }
    }
}