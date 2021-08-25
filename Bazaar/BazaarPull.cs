using System;
using System.Collections.Generic;
using MessagePack;

namespace dev
{
    [MessagePackObject]
    public class BazaarPull
    {
        public BazaarPull() { }
        [Key(0)]
        public int Id { get; set; }
        [Key(1)]
        public List<ProductInfo> Products { get; set; }
        [Key(2)]
        public DateTime Timestamp { get; set; }
    }
}