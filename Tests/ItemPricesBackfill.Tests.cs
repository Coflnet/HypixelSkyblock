using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    /// <summary>
    /// The backward walk of the price backfill, called by name like <see cref="ItemPricesTests"/> does
    /// </summary>
    public class ItemPricesBackfillTests
    {
        private static readonly DateTime Earliest = new DateTime(2026, 10, 12);
        private static readonly DateTime Floor = new DateTime(2019, 6, 1);

        private static AveragePrice Row(DateTime date) => new AveragePrice { ItemId = 1, Date = date, Avg = 5 };

        private class Harness
        {
            public List<(DateTime start, DateTime end)> Windows = new();
            public List<AveragePrice> Saved = new();
            public int SaveCalls;
            public Func<DateTime, DateTime, List<AveragePrice>> Source = (s, e) => new();

            public Task<List<AveragePrice>> Load(DateTime s, DateTime e)
            {
                Windows.Add((s, e));
                return Task.FromResult(Source(s, e));
            }

            public Task Save(List<AveragePrice> rows)
            {
                SaveCalls++;
                Saved.AddRange(rows);
                return Task.CompletedTask;
            }
        }

        private static async Task<int> Walk(Harness h, DateTime earliest, DateTime floor, CancellationToken token = default)
        {
            var method = typeof(ItemPrices).GetMethod("BackfillBeforeEarliest", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "ItemPrices.BackfillBeforeEarliest is missing");
            Func<DateTime, DateTime, Task<List<AveragePrice>>> load = h.Load;
            Func<List<AveragePrice>, Task> save = h.Save;
            try
            {
                return await (Task<int>)method.Invoke(null, new object[] { earliest, load, save, floor, token });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        [Test]
        public async Task ThreeWeeksOfSalesThenEmptyStops()
        {
            var h = new Harness();
            h.Source = (s, e) => s >= Earliest.AddDays(-21) ? new() { Row(s) } : new();
            var written = await Walk(h, Earliest.AddHours(5), Floor);

            Assert.That(written, Is.EqualTo(3));
            Assert.That(h.SaveCalls, Is.EqualTo(3));
            Assert.That(h.Windows.Count, Is.EqualTo(4));
            Assert.That(h.Windows[0].end, Is.EqualTo(Earliest));
            for (int i = 0; i < h.Windows.Count; i++)
            {
                Assert.That(h.Windows[i].end - h.Windows[i].start, Is.EqualTo(TimeSpan.FromDays(7)));
                if (i > 0)
                    Assert.That(h.Windows[i].end, Is.EqualTo(h.Windows[i - 1].start));
            }
        }

        [Test]
        public async Task EmptyFirstWindowWritesNothing()
        {
            var h = new Harness();
            var written = await Walk(h, Earliest, Floor);
            Assert.That(written, Is.EqualTo(0));
            Assert.That(h.SaveCalls, Is.EqualTo(0));
            Assert.That(h.Windows.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task RowsAtOrAfterEarliestAreNotSaved()
        {
            var h = new Harness();
            var first = true;
            h.Source = (s, e) =>
            {
                if (!first) return new();
                first = false;
                return new() { Row(Earliest.AddDays(-1)), Row(Earliest), Row(Earliest.AddDays(1)) };
            };
            var written = await Walk(h, Earliest, Floor);
            Assert.That(written, Is.EqualTo(1));
            Assert.That(h.Saved.Select(r => r.Date), Is.EqualTo(new[] { Earliest.AddDays(-1) }));
        }

        [Test]
        public async Task StopsAtFloorWhenSalesNeverEnd()
        {
            var h = new Harness();
            h.Source = (s, e) => new() { Row(s) };
            var floor = Earliest.AddDays(-30);
            var written = await Walk(h, Earliest, floor);
            Assert.That(written, Is.EqualTo(h.Windows.Count));
            Assert.That(h.Windows.Min(w => w.start), Is.EqualTo(floor));
            Assert.That(h.Windows.Count, Is.EqualTo(5));
        }

        [Test]
        public void CancellationIsHonoured()
        {
            var h = new Harness();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(
                () => Walk(h, Earliest, Floor, cts.Token));
            Assert.That(h.Windows, Is.Empty);
        }
    }
}
