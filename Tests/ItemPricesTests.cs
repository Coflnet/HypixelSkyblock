using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
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

        [Test]
        public async Task BackfillFillsOnlyMissingDaysOfWindow()
        {
            var start = new DateTime(2026, 8, 3);
            var end = start.AddDays(7);
            var window = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();
            // aggregation stopped after 2026-08-06, the rest of the week stayed empty
            var partly = window.Take(4).ToList();
            var calls = new List<(DateTime from, DateTime to)>();
            Func<DateTime, DateTime, Task<IEnumerable<AveragePrice>>> aggregate = (from, to) =>
            {
                calls.Add((from, to));
                return Task.FromResult(window.Select(d => new AveragePrice { Date = d, ItemId = 1 }));
            };

            var filled = await Invoke<List<AveragePrice>>("MissingDayPrices", partly, start, end, aggregate);
            Assert.That(filled.Select(p => p.Date), Is.EqualTo(window.Skip(4)));
            Assert.That(calls, Is.EqualTo(new[] { (start.AddDays(4), end) }));

            calls.Clear();
            var none = await Invoke<List<AveragePrice>>("MissingDayPrices", window, start, end, aggregate);
            Assert.That(none, Is.Null);
            Assert.That(calls, Is.Empty);

            var all = await Invoke<List<AveragePrice>>("MissingDayPrices", new List<DateTime>(), start, end, aggregate);
            Assert.That(all.Select(p => p.Date), Is.EqualTo(window));
            Assert.That(calls, Is.EqualTo(new[] { (start, end) }));
        }

        [Test]
        public async Task BackfillWindowFillsRestOfPartlyFilledWeek()
        {
            var start = new DateTime(2026, 8, 3);
            var window = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();
            using var context = new FakeContext(
                window.Take(4).Select(d => new AveragePrice { Date = d, ItemId = 1 }),
                window.Select(d => new SaveAuction { ItemId = 1, End = d.AddHours(12), HighestBidAmount = 100, Count = 1 }));

            await Invoke<bool>("BackfillWindow", context, 1, start, start.AddDays(7));

            Assert.That(context.Prices.Local.Skip(4).Select(p => p.Date), Is.EqualTo(window.Skip(4)));
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
        /// Context over in-memory sets that never connects, added prices are collected in <see cref="FakeSet{T}.Local"/>
        /// </summary>
        private class FakeContext : HypixelContext
        {
            public new FakeSet<AveragePrice> Prices { get; }

            public FakeContext(IEnumerable<AveragePrice> prices, IEnumerable<SaveAuction> auctions)
            {
                Prices = new FakeSet<AveragePrice>(prices);
                base.Prices = Prices;
                base.Auctions = new FakeSet<SaveAuction>(auctions);
            }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        }

        private class FakeSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T> where T : class
        {
            public new List<T> Local { get; }
            private AsyncQuery<T> Query => new(Local);

            public FakeSet(IEnumerable<T> items) => Local = items.ToList();

            public override IEntityType EntityType => throw new NotSupportedException();
            Type IQueryable.ElementType => typeof(T);
            Expression IQueryable.Expression => ((IQueryable)Query).Expression;
            IQueryProvider IQueryable.Provider => Query;
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => Local.GetEnumerator();
            IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) => Query.GetAsyncEnumerator(cancellationToken);

            public override Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            {
                Local.AddRange(entities);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Linq to objects source that EF's ToListAsync and AnyAsync accept
        /// </summary>
        private class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>, IAsyncQueryProvider
        {
            public AsyncQuery(IEnumerable<T> items) : base(items) { }
            public AsyncQuery(Expression expression) : base(expression) { }

            IQueryProvider IQueryable.Provider => this;
            IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) => new AsyncQuery<TElement>(expression);

            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
            {
                // TResult is Task<X>, run the query synchronously and wrap its X
                var resultType = typeof(TResult).GetGenericArguments()[0];
                var result = typeof(IQueryProvider).GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })
                    .MakeGenericMethod(resultType).Invoke(this, new object[] { expression });
                return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult)).MakeGenericMethod(resultType).Invoke(null, new[] { result });
            }

            public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                await Task.Yield();
                foreach (var item in (IEnumerable<T>)this)
                    yield return item;
            }
        }
    }
}
