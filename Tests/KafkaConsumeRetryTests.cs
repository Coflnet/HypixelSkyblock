using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Kafka;
using Confluent.Kafka;
using NUnit.Framework;
using SkyConsumer = Coflnet.Sky.Kafka.KafkaConsumer;

namespace Coflnet.Sky.Core.Tests;

// Retry loop of KafkaConsumer.Consume<T>, driven through the plain and the renewing batch loop over fakes,
// with an injected delay and clock.
[NonParallelizable]
public class KafkaConsumeRetryTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly string[] Topics = { "renewal-test" };
    private static readonly Prometheus.Counter ProcessFail =
        Prometheus.Metrics.CreateCounter("consume_process_failed", "How often processing of consumed messages failed");

    private sealed class ManualClock : TimeProvider
    {
        private long ticks = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero).UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan by) => Interlocked.Add(ref ticks, by.Ticks);
    }

    private static int[] Seconds(IEnumerable<TimeSpan> delays) => delays.Select(d => (int)d.TotalSeconds).ToArray();

    private static FakeConsumer Consumer(params long[] offsets)
    {
        var fake = FakeConsumer.Create("generation", new ConcurrentQueue<string>());
        fake.Assignment = new List<TopicPartition> { new("renewal-test", 0) };
        foreach (var offset in offsets)
            fake.Records.Enqueue(FakeConsumer.Record(offset));
        return fake;
    }

    // One renewing generation over a fresh handle that replays the given (uncommitted) offsets.
    private static Task RenewingGeneration(Func<IEnumerable<string>, Task> handler, CancellationToken token, FakeConsumer fake)
    {
        var handoff = new FakeHandoff { Start = () => fake.Consumer };
        return SkyConsumer.ConsumeRenewingBatches(handoff, Topics, handler, 1, token);
    }

    private static Task FailingPreflight(Func<IEnumerable<string>, Task> handler, CancellationToken token)
    {
        var handoff = new FakeHandoff { Start = () => throw new InvalidOperationException("synthetic preflight failure") };
        return SkyConsumer.ConsumeRenewingBatches(handoff, Topics, handler, 1, token);
    }

    // One plain (flag-off) generation: it commits each record's own offset, so 41 is replayed before the poison 42.
    private static Task PlainReplayGeneration(Func<IEnumerable<string>, Task> handler, CancellationToken token)
    {
        var fake = Consumer(41, 42);
        return SkyConsumer.ConsumeBatchPlain<string>(new ConsumerConfig(), Topics, handler, token, 1, _ => fake.Consumer);
    }

    // Records each requested delay and cancels on the given one, which then never completes on its own.
    private static Func<TimeSpan, CancellationToken, Task> RecordingDelay(ConcurrentQueue<TimeSpan> delays, CancellationTokenSource stop, int cancelOn)
    {
        return (delay, token) =>
        {
            delays.Enqueue(delay);
            if (delays.Count < cancelOn)
                return Task.CompletedTask;
            stop.Cancel();
            return Task.Delay(Timeout.Infinite, token);
        };
    }

    private static Task Poison(string value) => value == "42"
        ? throw new InvalidOperationException("synthetic poison message")
        : Task.CompletedTask;

    [Test]
    public async Task ThrowingConsumerBacksOffFromTwoSecondsDoublingToSixty()
    {
        using var stop = new CancellationTokenSource();
        var delays = new ConcurrentQueue<TimeSpan>();
        var failuresBefore = ProcessFail.Value;
        await SkyConsumer.ConsumeRetrying<string>(FailingPreflight, "renewal-test", _ => Task.CompletedTask, stop.Token,
            RecordingDelay(delays, stop, 8)).WaitAsync(Wait);
        Assert.That(Seconds(delays), Is.EqualTo(new[] { 2, 4, 8, 16, 32, 60, 60, 60 }));
        Assert.That(ProcessFail.Value - failuresBefore, Is.EqualTo(8));
    }

    [Test]
    public async Task NormalReturnWhileNotCancelledBacksOffInsteadOfReenteringImmediately()
    {
        using var stop = new CancellationTokenSource();
        var delays = new ConcurrentQueue<TimeSpan>();
        var generations = 0;
        // the renewing path logs a poison message and returns normally
        Func<Func<IEnumerable<string>, Task>, CancellationToken, Task> poisoned = (handler, token) =>
        {
            Interlocked.Increment(ref generations);
            return RenewingGeneration(handler, token, Consumer(42));
        };
        await SkyConsumer.ConsumeRetrying<string>(poisoned, "renewal-test", Poison, stop.Token,
            RecordingDelay(delays, stop, 3)).WaitAsync(Wait);
        Assert.That(Seconds(delays), Is.EqualTo(new[] { 2, 4, 8 }));
        Assert.That(generations, Is.EqualTo(3), "one rebuild per delay, never an immediate re-entry");
    }

    [Test]
    public async Task PlainPathReplayOfTheLastGoodRecordDoesNotResetTheBackoff()
    {
        using var stop = new CancellationTokenSource();
        var delays = new ConcurrentQueue<TimeSpan>();
        await SkyConsumer.ConsumeRetrying<string>(PlainReplayGeneration, "renewal-test", Poison, stop.Token,
            RecordingDelay(delays, stop, 3), new ManualClock()).WaitAsync(Wait);
        Assert.That(Seconds(delays), Is.EqualTo(new[] { 2, 4, 8 }));
    }

    [Test]
    public async Task GenerationThatProcessedABatchAndRanSixtySecondsResetsTheBackoff()
    {
        using var stop = new CancellationTokenSource();
        var delays = new ConcurrentQueue<TimeSpan>();
        var clock = new ManualClock();
        var generation = 0;
        Func<Func<IEnumerable<string>, Task>, CancellationToken, Task> consume = (handler, token) =>
        {
            generation++;
            return PlainReplayGeneration(handler, token);
        };
        Func<string, Task> action = value =>
        {
            if (generation == 3 && value == "41")
                clock.Advance(TimeSpan.FromSeconds(60)); // the third generation stays up for a minute
            return Poison(value);
        };
        await SkyConsumer.ConsumeRetrying(consume, "renewal-test", action, stop.Token, RecordingDelay(delays, stop, 4), clock)
            .WaitAsync(Wait);
        Assert.That(Seconds(delays), Is.EqualTo(new[] { 2, 4, 2, 4 }));
    }

    [Test]
    public void LongGenerationWithoutAProcessedBatchKeepsGrowingTheBackoff()
    {
        var clock = new ManualClock();
        var backoff = new ConsumeBackoff(clock);
        var delays = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            backoff.GenerationStarted();
            clock.Advance(TimeSpan.FromMinutes(5));
            delays.Add(backoff.NextDelay());
        }
        Assert.That(Seconds(delays), Is.EqualTo(new[] { 2, 4, 8 }));
        Assert.That(backoff.Attempt, Is.EqualTo(3));
    }

    [Test]
    public async Task CancellationDuringTheBackoffCancelsTheDelayAndReturnsWithoutError()
    {
        using var stop = new CancellationTokenSource();
        var delays = new ConcurrentQueue<TimeSpan>();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<TimeSpan, CancellationToken, Task> realDelay = (delay, token) =>
        {
            delays.Enqueue(delay);
            waiting.TrySetResult();
            return Task.Delay(TimeSpan.FromHours(1), token);
        };
        var consuming = SkyConsumer.ConsumeRetrying<string>(FailingPreflight, "renewal-test", _ => Task.CompletedTask,
            stop.Token, realDelay);
        await waiting.Task.WaitAsync(Wait);
        stop.Cancel();
        await consuming.WaitAsync(Wait);
        Assert.That(consuming.IsCompletedSuccessfully, Is.True);
        Assert.That(Seconds(delays), Is.EqualTo(new[] { 2 }));
    }

    // A plain-path consumer that cannot be built or subscribed must fault ConsumeBatch, not hang it, so Consume<T> can retry.
    [TestCase(true)]
    [TestCase(false)]
    public void PlainBatchSurfacesABuildOrSubscribeFailureInsteadOfHanging(bool failBuild)
    {
        var fake = FakeConsumer.Create("plain", new ConcurrentQueue<string>());
        fake.OnSubscribe = () => throw new InvalidOperationException("synthetic subscribe failure");
        Func<ConsumerConfig, IConsumer<Ignore, string>> build = failBuild
            ? _ => throw new InvalidOperationException("synthetic build failure")
            : _ => fake.Consumer;
        Assert.ThrowsAsync<InvalidOperationException>(() => SkyConsumer.ConsumeBatchPlain<string>(new ConsumerConfig(), Topics,
            _ => Task.CompletedTask, CancellationToken.None, 1, build).WaitAsync(Wait));
        if (!failBuild)
            Assert.That(fake.Disposals, Is.EqualTo(1), "the handle that failed to subscribe is still disposed");
    }

    [Test]
    public async Task CancellationWhileConsumingReturnsWithoutLoggingAnErrorOrWaiting()
    {
        using var stop = new CancellationTokenSource();
        var delays = 0;
        var failuresBefore = ProcessFail.Value;
        var idle = Consumer();
        var consumingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        idle.OnConsume = () => consumingStarted.TrySetResult();
        var output = new StringWriter();
        var console = Console.Out;
        Console.SetOut(output);
        try
        {
            // the idle consumer blocks its thread in Consume until the caller cancels (production runs it LongRunning)
            var consuming = Task.Run(() => SkyConsumer.ConsumeRetrying<string>((handler, token) => RenewingGeneration(handler, token, idle),
                "renewal-test", _ => Task.CompletedTask, stop.Token, (_, _) => { Interlocked.Increment(ref delays); return Task.CompletedTask; }));
            await consumingStarted.Task.WaitAsync(Wait);
            stop.Cancel();
            await consuming.WaitAsync(Wait);
        }
        finally
        {
            Console.SetOut(console);
        }
        Assert.That(delays, Is.Zero);
        Assert.That(ProcessFail.Value, Is.EqualTo(failuresBefore));
        Assert.That(output.ToString(), Does.Not.Contain("Error:"));
    }

    [Test]
    public async Task FailureAfterTheCallerCancelledEndsTheLoopWithoutRetryOrError()
    {
        using var stop = new CancellationTokenSource();
        var delays = 0;
        var failuresBefore = ProcessFail.Value;
        Func<Func<IEnumerable<string>, Task>, CancellationToken, Task> failsOnStop = (_, _) =>
        {
            stop.Cancel();
            throw new InvalidOperationException("synthetic close failure while stopping");
        };
        var consuming = SkyConsumer.ConsumeRetrying<string>(failsOnStop, "renewal-test", _ => Task.CompletedTask, stop.Token,
            (_, _) => { delays++; return Task.CompletedTask; });
        await consuming.WaitAsync(Wait);
        Assert.That(consuming.IsCompletedSuccessfully, Is.True);
        Assert.That(delays, Is.Zero);
        Assert.That(ProcessFail.Value, Is.EqualTo(failuresBefore));
    }
}
