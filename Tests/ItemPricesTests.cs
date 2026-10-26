using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    /// <summary>
    /// The loops run against MariaDB, these tests call their non-public steps by name
    /// </summary>
    public class ItemPricesTests
    {
        [Test]
        public async Task FailingItemDoesNotStopLaterItems()
        {
            var items = new Dictionary<string, int> { { "HYPERION", 1 }, { "GARDEN_WARRIOR_FERMENTO", 2 }, { "TERMINATOR", 3 } };
            var processed = new List<int>();
            Func<KeyValuePair<string, int>, Task> action = item =>
            {
                if (item.Value == 2)
                    throw new InvalidOperationException("Nullable object must have a value.");
                processed.Add(item.Value);
                return Task.CompletedTask;
            };

            var failed = await Invoke<int>("ForEachItem", items, "test", action);

            Assert.That(processed, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(failed, Is.EqualTo(1));
        }

        [Test]
        public async Task DailyJobFillsGapOfSeveralDaysDespiteFailingDay()
        {
            var today = new DateTime(2026, 10, 26);
            // rows exist up to 2026-10-19, the job was down for the 6 days after
            var existing = Enumerable.Range(7, 8).Select(i => today.AddDays(-i)).ToList();
            var failingDay = today.AddDays(-4);
            var filled = new List<DateTime>();
            Func<DateTime, Task<int>> fillDay = day =>
            {
                if (day == failingDay)
                    throw new InvalidOperationException("Nullable object must have a value.");
                filled.Add(day);
                return Task.FromResult(1);
            };

            var added = await Invoke<int>("FillMissingDays", new KeyValuePair<string, int>("HYPERION", 1), existing, today, 14, fillDay);

            var expected = Enumerable.Range(1, 6).Select(i => today.AddDays(-i)).Where(d => d != failingDay).OrderBy(d => d);
            Assert.That(filled, Is.EqualTo(expected));
            Assert.That(added, Is.EqualTo(5));
        }

        [Test]
        public async Task AggregationIgnoresAuctionsWithZeroCount()
        {
            // MariaDB returns NULL for HighestBidAmount / 0 which crashed materialization, linq to objects throws instead
            var auctions = new AsyncQuery<SaveAuction>(new List<SaveAuction>
            {
                new() { End = new DateTime(2026, 8, 6, 10, 0, 0), HighestBidAmount = 500, Count = 0 },
                new() { End = new DateTime(2026, 8, 7, 10, 0, 0), HighestBidAmount = 100, Count = 2 },
            });

            IEnumerable<AveragePrice> result = null;
            Assert.That(async () => result = await Invoke<IEnumerable<AveragePrice>>("AvgFromAuctions", 5, auctions, false), Throws.Nothing);

            Assert.That(result.Select(r => (r.Date, r.Avg, r.Volume)), Is.EqualTo(new[] { (new DateTime(2026, 8, 7), 50d, 2) }));
        }

        private static async Task<T> Invoke<T>(string name, params object[] args)
        {
            var method = typeof(ItemPrices).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"ItemPrices.{name} is missing");
            try
            {
                return await (Task<T>)method.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// Linq to objects source that EF's ToListAsync accepts
        /// </summary>
        private class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>, IQueryProvider
        {
            public AsyncQuery(IEnumerable<T> items) : base(items) { }
            public AsyncQuery(Expression expression) : base(expression) { }

            IQueryProvider IQueryable.Provider => this;
            IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) => new AsyncQuery<TElement>(expression);

            public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                await Task.Yield();
                foreach (var item in (IEnumerable<T>)this)
                    yield return item;
            }
        }
    }
}
