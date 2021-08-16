using System;
using System.Collections.Generic;
using System.Linq;
using Hypixel.NET.SkyblockApi.Bazaar;
using MessagePack;

namespace dev
{
    [MessagePackObject]
    public class BazaarPull
    {
        public BazaarPull() { }
        public BazaarPull(GetBazaarProducts result)
        {
            this.Timestamp = result.LastUpdated;
            this.Products = result.Products.Select(p => new ProductInfo(p.Value, this)).ToList();
        }
        [Key(0)]
        public int Id { get; set; }
        [Key(1)]
        public List<ProductInfo> Products { get; set; }
        [Key(2)]
        public DateTime Timestamp { get; set; }
    }
}