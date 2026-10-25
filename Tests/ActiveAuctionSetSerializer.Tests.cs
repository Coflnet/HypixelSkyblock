using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class ActiveAuctionSetSerializerTests
    {
        [Test]
        public async Task RoundTrips()
        {
            var source = Enumerable.Range(1, 100).Select(i => ActiveAuctionTestData.Make(i, bids: i % 3)).ToList();
            using var ms = new MemoryStream();
            await ActiveAuctionSetSerializer.SerializeAsync(ms, source, CancellationToken.None);
            ms.Position = 0;
            var result = new System.Collections.Generic.List<SaveAuction>();
            await foreach (var a in ActiveAuctionSetSerializer.DeserializeAsync(ms, CancellationToken.None))
                result.Add(a);
            Assert.That(result.Count, Is.EqualTo(100));
            for (int i = 0; i < 100; i++)
            {
                var e = source[i];
                var r = result[i];
                Assert.That(r.Uuid, Is.EqualTo(e.Uuid));
                Assert.That(r.UId, Is.EqualTo(e.UId));
                Assert.That(r.Tag, Is.EqualTo(e.Tag));
                Assert.That(r.ItemName, Is.EqualTo(e.ItemName));
                Assert.That(r.Start, Is.EqualTo(e.Start));
                Assert.That(r.End, Is.EqualTo(e.End));
                Assert.That(r.StartingBid, Is.EqualTo(e.StartingBid));
                Assert.That(r.HighestBidAmount, Is.EqualTo(e.HighestBidAmount));
                Assert.That(r.Bin, Is.EqualTo(e.Bin));
                Assert.That(r.Tier, Is.EqualTo(e.Tier));
                Assert.That(r.Category, Is.EqualTo(e.Category));
                Assert.That(r.Reforge, Is.EqualTo(e.Reforge));
                Assert.That(r.Count, Is.EqualTo(e.Count));
                Assert.That(r.AuctioneerId, Is.EqualTo(e.AuctioneerId));
                Assert.That(r.ProfileId, Is.EqualTo(e.ProfileId));
                Assert.That(r.ItemId, Is.EqualTo(e.ItemId));
                Assert.That(r.Enchantments.Select(x => (x.Type, x.Level)), Is.EqualTo(e.Enchantments.Select(x => (x.Type, x.Level))));
                Assert.That(r.Bids.Select(x => (x.Bidder, x.Amount, x.Timestamp)), Is.EqualTo(e.Bids.Select(x => (x.Bidder, x.Amount, x.Timestamp))));
                Assert.That(r.NbtData.data, Is.EqualTo(e.NbtData.data));
                Assert.That(r.FlatenedNBT, Is.EquivalentTo(e.FlatenedNBT));
            }
        }

        [Test]
        public async Task EmptyRoundTrips()
        {
            using var ms = new MemoryStream();
            await ActiveAuctionSetSerializer.SerializeAsync(ms, new SaveAuction[0], CancellationToken.None);
            ms.Position = 0;
            var count = 0;
            await foreach (var _ in ActiveAuctionSetSerializer.DeserializeAsync(ms, CancellationToken.None))
                count++;
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public async Task MultipleChunksRoundTrip()
        {
            var source = Enumerable.Range(1, 4500).Select(i => ActiveAuctionTestData.Make(i)).ToList();
            using var ms = new MemoryStream();
            await ActiveAuctionSetSerializer.SerializeAsync(ms, source, CancellationToken.None);
            ms.Position = 0;
            var uids = new System.Collections.Generic.List<long>();
            await foreach (var a in ActiveAuctionSetSerializer.DeserializeAsync(ms, CancellationToken.None))
                uids.Add(a.UId);
            Assert.That(uids, Is.EqualTo(source.Select(a => a.UId)));
        }
    }
}
