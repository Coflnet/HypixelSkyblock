using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class ActiveAuctionSetMemoryTests
    {
        [Test]
        public void BytesPerAuctionStayBelowBudget()
        {
            var set = new ActiveAuctionSet();
            GC.Collect();
            var before = GC.GetTotalMemory(true);
            // freshly built (as if just deserialized) then normalized by the set; the list keeps them alive
            var list = Enumerable.Range(1, 1000).Select(i => ActiveAuctionTestData.Make(i, itemNameLen: 44)).ToList();
            foreach (var a in list)
                set.Upsert(a, "mem");
            GC.Collect();
            var after = GC.GetTotalMemory(true);
            var perAuction = (after - before) / 1000.0;
            TestContext.WriteLine($"measured {perAuction:F0} bytes/auction");
            GC.KeepAlive(list);
            GC.KeepAlive(set);
            Assert.That(set.Count, Is.EqualTo(1000));
            Assert.That(perAuction, Is.LessThan(3000));
        }
    }
}
