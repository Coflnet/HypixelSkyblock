using System;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class ActiveAuctionDbLoaderTests
    {
        private static readonly DateTime Base = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const int MaxId = 10_000_000;

        private class FakeLoader : ActiveAuctionDbLoader
        {
            public int Probes;
            public bool Fail;
            protected override Task<int> MaxIdAsync(CancellationToken ct) => Task.FromResult(MaxId);
            protected override Task<(int Id, DateTime Start)?> ProbeAsync(int fromId, CancellationToken ct)
            {
                Probes++;
                if (Fail)
                    throw new InvalidOperationException("db down");
                if (fromId > MaxId)
                    return Task.FromResult<(int, DateTime)?>(null);
                return Task.FromResult<(int, DateTime)?>((fromId, Base.AddSeconds(fromId * 7L)));
            }
        }

        [Test]
        public async Task FindsBoundWithinMargin()
        {
            var loader = new FakeLoader();
            var notBefore = Base.AddSeconds(7L * 7_000_000);
            var found = await loader.FindLowerBoundIdAsync(notBefore, CancellationToken.None);
            var trueFirst = 7_000_001;
            Assert.That(found, Is.EqualTo(trueFirst - 50_000).Within(1));
            Assert.That(found, Is.LessThanOrEqualTo(trueFirst));
            Assert.That(loader.Probes, Is.LessThanOrEqualTo(30));
        }

        [Test]
        public async Task FailingProbeFallsBack()
        {
            var loader = new FakeLoader { Fail = true };
            var found = await loader.FindLowerBoundIdAsync(Base, CancellationToken.None);
            Assert.That(found, Is.EqualTo(MaxId - 4_500_000));
        }
    }
}
