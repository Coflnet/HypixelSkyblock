using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Kafka;
using Confluent.Kafka;
using MessagePack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SkyCreator = Coflnet.Sky.Kafka.KafkaCreator;
using SkySerializers = Coflnet.Sky.Core.SerializerFactory;
using CoreSerializers = Coflnet.Kafka.SerializerFactory;

namespace Coflnet.Sky.Core.Tests;

// Opt-in Kafka client certificate renewal (KAFKA:TLS:RELOAD_ON_CHANGE) of SkyCore's producers and consumers.
[NonParallelizable]
public class KafkaRenewalTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly TopicPartition Partition0 = new("renewal-test", 0);

    private static SaveAuction Auction() => new()
    {
        Uuid = "4a3f0c2e8d1b4f6a9c7e5d3b1a2f4e6d", Tag = "HYPERION", StartingBid = 1_000_000, Count = 1,
        Start = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc), End = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc)
    };

    private static void AssertSameBytes<T>(T value)
    {
        var context = SerializationContext.Empty;
        Assert.That(CoreSerializers.GetSerializer<T>().Serialize(value, context),
            Is.EqualTo(SkySerializers.GetSerializer<T>().Serialize(value, context)), typeof(T).Name);
    }

    [Test]
    public void SkyAndCoreMsgPackSerializersProduceIdenticalBytes()
    {
        AssertSameBytes(Auction());
        AssertSameBytes("synthetic key");
        AssertSameBytes(42L);
        AssertSameBytes(new Dictionary<string, int> { ["a"] = 1 });
        AssertSameBytes<Ignore>(null);
    }

    private sealed class ProbeBuilder<TKey, TValue>() : ProducerBuilder<TKey, TValue>(new ProducerConfig())
    {
        public ISerializer<TKey> Key => KeySerializer;
        public ISerializer<TValue> Value => ValueSerializer;
    }

    [Test]
    public void RenewalRecipeSetsThePlainPathMsgPackSerializersOnTheSameBuilderBeforeConfigure()
    {
        var probe = new ProbeBuilder<string, SaveAuction>();
        var configuredWithSerializers = false;
        var recipe = SkyCreator.WithSerializers<string, SaveAuction>(true, builder =>
        {
            configuredWithSerializers = probe.Key != null && probe.Value != null;
            return builder;
        });
        Assert.That(recipe(probe), Is.SameAs(probe), "Coflnet.Core only renews when the recipe keeps its builder.");
        var context = SerializationContext.Empty;
        var auction = Auction(); // one instance: SaveAuction stamps a construction time
        Assert.Multiple(() =>
        {
            Assert.That(configuredWithSerializers, Is.True);
            Assert.That(probe.Key.Serialize("key", context), Is.EqualTo(MessagePackSerializer.Serialize("key")));
            Assert.That(probe.Value.Serialize(auction, context), Is.EqualTo(MessagePackSerializer.Serialize(auction)));
        });
        var plain = new ProbeBuilder<string, string>();
        SkyCreator.WithSerializers<string, string>(false, null)(plain);
        Assert.That(plain.Key, Is.Null);
        Assert.That(plain.Value, Is.Null);
    }

    private static IConfiguration KafkaSettings(bool reload) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
    {
        ["KAFKA:BROKERS"] = "127.0.0.1:1", // closed loopback port; nothing is produced
        ["KAFKA:TLS:RELOAD_ON_CHANGE"] = reload ? "true" : "false"
    }).Build();

    [TestCase(true)]
    [TestCase(false)]
    public void BuildProducerDelegatesToCoflnetCoreOnlyWithTheFlag(bool reload)
    {
        Type builderType = null;
        var creator = new SkyCreator(NullLogger<SkyCreator>.Instance, KafkaSettings(reload));
        using var producer = creator.BuildProducer<string, string>(configure: builder =>
        {
            builderType = builder.GetType();
            return builder;
        });
        Assert.That(producer, Is.Not.Null);
        if (reload)
            Assert.That(builderType.Namespace, Is.EqualTo("Coflnet.Kafka"), "Coflnet.Core's renewal recipe builder");
        else
            Assert.That(builderType, Is.EqualTo(typeof(ProducerBuilder<string, string>)));
    }

    [TestCase("true", true)]
    [TestCase(" True ", true)]
    [TestCase("false", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("yes", false)]
    [TestCase("1", false)]
    public void RenewalFlagUsesBoolTryParseAndTreatsAnythingElseAsOff(string value, bool on)
    {
        Assert.That(KafkaRenewalFlag.IsOn(value), Is.EqualTo(on));
    }

    [Test]
    public void InvalidFlagKeepsTheProducerOnThePlainPath()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["KAFKA:BROKERS"] = "127.0.0.1:1", ["KAFKA:TLS:RELOAD_ON_CHANGE"] = "yes"
        }).Build();
        Type builderType = null;
        using var producer = new SkyCreator(NullLogger<SkyCreator>.Instance, settings)
            .BuildProducer<string, string>(configure: builder => { builderType = builder.GetType(); return builder; });
        Assert.That(builderType, Is.EqualTo(typeof(ProducerBuilder<string, string>)));
    }

    private static ConsumerConfig CallerConnection() => new()
    {
        BootstrapServers = "caller-broker:9093", SaslUsername = "caller-user", SaslPassword = "caller-password",
        SslCaLocation = "/caller/ca.crt", SslCertificateLocation = "/caller/client.crt", SslKeyLocation = "/caller/client.key"
    };

    [Test]
    public void AmbientRenewalTakesOnlyTheFlagFromSimplerConfigAndTheConnectionFromTheCaller()
    {
        var caller = CallerConnection();
        Assert.That(Coflnet.Sky.Kafka.KafkaConsumer.AmbientRenewalConfiguration(caller, _ => null), Is.Null);
        Assert.That(Coflnet.Sky.Kafka.KafkaConsumer.AmbientRenewalConfiguration(caller, _ => throw new FormatException("malformed custom.conf.json")),
            Is.Null, "An unreadable SimplerConfig keeps the default path.");
        Assert.That(Coflnet.Sky.Kafka.KafkaConsumer.AmbientRenewalConfiguration(caller, key => key.EndsWith("RELOAD_ON_CHANGE") ? "false" : "x"), Is.Null);
        var settings = Coflnet.Sky.Kafka.KafkaConsumer.AmbientRenewalConfiguration(caller, key => key.EndsWith("RELOAD_ON_CHANGE") ? "true" : "ambient-" + key);
        var baseline = Coflnet.Kafka.KafkaCreator.GetClientConfig(settings);
        Assert.Multiple(() =>
        {
            Assert.That(baseline.BootstrapServers, Is.EqualTo("caller-broker:9093"));
            Assert.That(baseline.SaslUsername, Is.EqualTo("caller-user"));
            Assert.That(baseline.SaslPassword, Is.EqualTo("caller-password"));
            Assert.That(baseline.SecurityProtocol, Is.EqualTo(SecurityProtocol.SaslSsl));
            Assert.That(settings["KAFKA:TLS:CA_LOCATION"], Is.EqualTo("/caller/ca.crt"));
            Assert.That(settings["KAFKA:TLS:CERTIFICATE_LOCATION"], Is.EqualTo("/caller/client.crt"));
            Assert.That(settings["KAFKA:TLS:KEY_LOCATION"], Is.EqualTo("/caller/client.key"));
            Assert.That(settings["KAFKA:TLS:RELOAD_ON_CHANGE"], Is.EqualTo("true"));
        });
    }

    // ---- ConsumeBatch / Consume on the renewing path ----

    private static FakeConsumer Assigned(string name, ConcurrentQueue<string> events)
    {
        var fake = FakeConsumer.Create(name, events);
        fake.Assignment = new List<TopicPartition> { Partition0 };
        return fake;
    }

    private static IConsumer<Ignore, string> Replace(IConsumer<Ignore, string> current, FakeConsumer next, ConcurrentQueue<string> events)
    {
        events.Enqueue("handoff");
        current.Close();
        current.Dispose();
        return next.Consumer;
    }

    [Test]
    public async Task RenewingBatchHandsOffOnlyAfterTheInflightBatchIsCommitted()
    {
        var events = new ConcurrentQueue<string>();
        var old = Assigned("old", events);
        var replacement = Assigned("new", events);
        old.Records.Enqueue(FakeConsumer.Record(42));
        var processing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var handoff = new FakeHandoff
        {
            Start = () => old.Consumer,
            Handoff = (current, buffered) =>
            {
                buffered(FakeConsumer.Record(43)); // fetched by the replacement while it was assigned
                return Replace(current, replacement, events);
            }
        };
        var consuming = Task.Run(() => Coflnet.Sky.Kafka.KafkaConsumer.ConsumeRenewingBatches<string>(handoff, new[] { "renewal-test" }, async batch =>
        {
            var values = string.Join(",", batch);
            events.Enqueue("action " + values);
            if (values == "42") { processing.TrySetResult(); await release.Task; }
            if (values == "43") stop.Cancel();
        }, 500, stop.Token));
        await processing.Task.WaitAsync(Wait);
        handoff.Request();
        await Task.Delay(50);
        release.TrySetResult();
        await consuming.WaitAsync(Wait);
        Assert.That(events, Is.EqualTo(new[] { "action 42", "old commit 0:43", "handoff", "old close", "action 43", "new commit 0:44", "new close" }));
        Assert.That(Prometheus.Metrics.CreateGauge("kafka_lag_renewal_test", "offset of kafka topic").Value, Is.EqualTo(50));
        Assert.That((old.Disposals, replacement.Disposals), Is.EqualTo((1, 1)));
    }

    [Test]
    public async Task RenewingBatchInterruptsAnIdleWaitForTheHandoff()
    {
        var events = new ConcurrentQueue<string>();
        var old = Assigned("old", events);
        var replacement = Assigned("new", events);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var handoff = new FakeHandoff { Start = () => old.Consumer, Handoff = (current, _) => Replace(current, replacement, events) };
        var consuming = Task.Run(() => Coflnet.Sky.Kafka.KafkaConsumer.ConsumeRenewingBatches<string>(handoff, new[] { "renewal-test" },
            _ => { stop.Cancel(); return Task.CompletedTask; }, 500, stop.Token));
        await Task.Delay(50);
        handoff.Request();
        while (handoff.Handoffs == 0 && !stop.IsCancellationRequested) await Task.Delay(5);
        replacement.Records.Enqueue(FakeConsumer.Record(7));
        await consuming.WaitAsync(Wait);
        Assert.That(events, Is.EqualTo(new[] { "handoff", "old close", "new commit 0:8", "new close" }));
    }

    [Test]
    public async Task RenewingBatchActionFailureStopsWithoutCommitLikeThePlainPath()
    {
        var events = new ConcurrentQueue<string>();
        var old = Assigned("old", events);
        old.Records.Enqueue(FakeConsumer.Record(42));
        var handoff = new FakeHandoff { Start = () => old.Consumer };
        await Coflnet.Sky.Kafka.KafkaConsumer.ConsumeRenewingBatches<string>(handoff, new[] { "renewal-test" },
            _ => throw new InvalidOperationException("synthetic action failure"), 500, CancellationToken.None).WaitAsync(Wait);
        Assert.That(events, Is.EqualTo(new[] { "old close" }));
        Assert.That(old.Disposals, Is.EqualTo(1));
    }

    [Test]
    public void RenewingBatchHandoffFailurePropagatesWithoutClosingTheHandleTwice()
    {
        var events = new ConcurrentQueue<string>();
        var old = Assigned("old", events);
        var handoff = new FakeHandoff
        {
            Start = () => old.Consumer,
            Handoff = (current, _) =>
            {
                current.Close();
                current.Dispose();
                throw new InvalidOperationException("Previous Kafka certificate expired during recovery.");
            }
        };
        handoff.Request();
        Assert.ThrowsAsync<InvalidOperationException>(() => Coflnet.Sky.Kafka.KafkaConsumer.ConsumeRenewingBatches<string>(handoff,
            new[] { "renewal-test" }, _ => Task.CompletedTask, 500, CancellationToken.None).WaitAsync(Wait));
        Assert.That((old.Closes, old.Disposals), Is.EqualTo((1, 1)));
    }

    // ---- ConsumePartitionedParallelBatch ----

    private sealed class PartitionedRun
    {
        public readonly ConcurrentQueue<string> Events = new();
        public readonly ConcurrentQueue<string> Batches = new();
        public readonly List<FakeConsumer> Handles = new();
        public readonly Queue<FakeConsumer> Next = new();
        public readonly CancellationTokenSource Stop = new(TimeSpan.FromSeconds(15));
        public int Revocations;
        public List<TopicPartition> Partitions = new() { Partition0 };
        // the loop's consumer factory: builds a handle wired to the loop's own partition handlers
        public Func<ConsumerConfig, Coflnet.Kafka.KafkaConsumerReadiness, IConsumer<Ignore, string>> Build;

        public FakeConsumer Add(string name, params long[] offsets)
        {
            var fake = FakeConsumer.Create(name, Events);
            foreach (var offset in offsets) fake.Records.Enqueue(FakeConsumer.Record(offset));
            Next.Enqueue(fake);
            return fake;
        }

        // the loop's handlers fire as librdkafka would: assignment on subscribe, revocation on close
        public IConsumer<Ignore, string> Create(ConsumerConfig settings, PartitionHandlers handlers)
        {
            var fake = Next.Dequeue();
            Handles.Add(fake);
            fake.OnSubscribe = () =>
            {
                fake.Assignment = Partitions.ToList();
                handlers.Assigned(Partitions.ToList());
            };
            fake.OnClose = () => handlers.Removed(fake.Assignment.ToList());
            return fake.Consumer;
        }

        public Task Run(Func<TopicPartition, IEnumerable<string>, Task> action, FakeHandoff handoff,
            Func<Func<ConsumerConfig, Coflnet.Kafka.KafkaConsumerReadiness, IConsumer<Ignore, string>>, IConsumerHandoff<string>> renewal = null)
        {
            if (handoff != null)
                renewal = build =>
                {
                    Build = build;
                    handoff.Start = () => Subscribed(build(new ConsumerConfig(), null));
                    return handoff;
                };
            return Task.Run(() => Coflnet.Sky.Kafka.KafkaConsumer.ConsumePartitionedParallelBatchThread(new ConsumerConfig(), new[] { "renewal-test" },
                action, 500, _ => Interlocked.Increment(ref Revocations), null, Create, renewal, Stop.Token));
        }

        public static IConsumer<Ignore, string> Subscribed(IConsumer<Ignore, string> consumer)
        {
            consumer.Subscribe("renewal-test");
            return consumer;
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(Wait);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    [Test]
    public async Task PartitionedPlainPathStillProcessesAndCommitsTheNextOffset()
    {
        var run = new PartitionedRun();
        var only = run.Add("plain", 10, 11);
        var consuming = run.Run((_, batch) =>
        {
            run.Batches.Enqueue(string.Join(",", batch));
            return Task.CompletedTask;
        }, handoff: null);
        await Until(() => run.Events.Contains("plain commit 0:12"));
        run.Stop.Cancel();
        await consuming.WaitAsync(Wait);
        Assert.That(run.Batches, Is.EqualTo(new[] { "10,11" }));
        Assert.That((only.Closes, only.Disposals), Is.EqualTo((1, 1)));
    }

    // A record the old handle fetched while draining is dropped with that handle and replayed by the replacement,
    // also when its Close reports no revocation.
    [TestCase(true)]
    [TestCase(false)]
    public async Task PartitionedHandoffDrainsTheInflightBatchCommitsAndContinuesOnTheReplacement(bool revokeOnClose)
    {
        var run = new PartitionedRun();
        var old = run.Add("old", 10, 11);
        var replacement = run.Add("new", 12);
        var processing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handoff = new FakeHandoff();
        var consuming = run.Run(async (_, batch) =>
        {
            var values = string.Join(",", batch);
            run.Batches.Enqueue(values);
            if (values == "10,11") { processing.TrySetResult(); await release.Task; }
        }, handoff);
        handoff.Handoff = (current, _) =>
        {
            run.Events.Enqueue("handoff");
            current.Close();
            current.Dispose();
            return PartitionedRun.Subscribed(run.Build(new ConsumerConfig(), null));
        };
        await processing.Task.WaitAsync(Wait);
        if (!revokeOnClose) old.OnClose = null;
        old.Records.Enqueue(FakeConsumer.Record(12));
        await Until(() => old.Records.IsEmpty);
        handoff.Request();
        await Task.Delay(50);
        Assert.That(run.Batches, Is.EqualTo(new[] { "10,11" }), "Nothing new is dispatched while draining.");
        release.TrySetResult();
        await Until(() => run.Events.Contains("new commit 0:13"));
        run.Stop.Cancel();
        await consuming.WaitAsync(Wait);
        Assert.That(run.Batches, Is.EqualTo(new[] { "10,11", "12" }));
        Assert.That(run.Events.Where(e => e.StartsWith("old") || e == "handoff"), Is.EqualTo(new[] { "old commit 0:12", "handoff", "old close" }));
        Assert.That((old.Closes, old.Disposals, replacement.Closes, replacement.Disposals), Is.EqualTo((1, 1, 1, 1)));
        Assert.That(handoff.Disposed, Is.True);
    }

    [Test]
    public async Task PartitionedHandoffRejectedBeforeCloseKeepsPendingRecordsAndResumes()
    {
        var run = new PartitionedRun();
        var old = run.Add("old", 10, 11);
        var processing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handoff = new FakeHandoff { Handoff = (current, _) => current };
        var consuming = run.Run(async (_, batch) =>
        {
            var values = string.Join(",", batch);
            run.Batches.Enqueue(values);
            if (values == "10,11") { processing.TrySetResult(); await release.Task; }
        }, handoff);
        await processing.Task.WaitAsync(Wait);
        old.Records.Enqueue(FakeConsumer.Record(12));
        await Until(() => old.Records.IsEmpty);
        handoff.Request();
        release.TrySetResult();
        await Until(() => run.Events.Contains("old commit 0:13"));
        Assert.That(old.Closes, Is.Zero);
        run.Stop.Cancel();
        await consuming.WaitAsync(Wait);
        Assert.That(run.Batches, Is.EqualTo(new[] { "10,11", "12" }));
        Assert.That(handoff.Handoffs, Is.EqualTo(1));
        Assert.That((old.Closes, old.Disposals), Is.EqualTo((1, 1)));
    }

    [Test]
    public void PartitionedHandoffFailurePropagatesSoTheHostRestarts()
    {
        var run = new PartitionedRun();
        var old = run.Add("old");
        var handoff = new FakeHandoff
        {
            Handoff = (current, _) =>
            {
                current.Close();
                current.Dispose();
                throw new InvalidOperationException("Previous Kafka certificate is no longer valid for recovery.");
            }
        };
        handoff.Request();
        Assert.ThrowsAsync<InvalidOperationException>(() => run.Run((_, _) => Task.CompletedTask, handoff).WaitAsync(Wait));
        Assert.That((old.Closes, old.Disposals), Is.EqualTo((1, 1)));
        Assert.That(handoff.Disposed, Is.True);
    }

    // Regression: records the first handle fetched while assigning waited for another record and could stall the partition.
    [Test]
    public async Task PartitionedLoopProcessesRecordsBufferedWhileTheFirstHandleWasAssigned()
    {
        var run = new PartitionedRun();
        run.Add("old");
        var handoff = new FakeHandoff();
        handoff.StartBuffered.Add(FakeConsumer.Record(10));
        var consuming = run.Run((_, batch) =>
        {
            run.Batches.Enqueue(string.Join(",", batch));
            return Task.CompletedTask;
        }, handoff);
        await Until(() => run.Events.Contains("old commit 0:11"));
        run.Stop.Cancel();
        await consuming.WaitAsync(Wait);
        Assert.That(run.Batches, Is.EqualTo(new[] { "10" }));
    }

    // Regression: while draining, a partition without a batch in flight kept fetching into an unbounded buffer.
    [Test]
    public async Task PartitionedDrainPausesIdlePartitionsAndResumesThemAfterARetainedHandoff()
    {
        var partition1 = new TopicPartition("renewal-test", 1);
        var run = new PartitionedRun { Partitions = new() { Partition0, partition1 } };
        var old = run.Add("old", 10);
        var processing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handoff = new FakeHandoff { Handoff = (current, _) => current };
        var consuming = run.Run(async (partition, batch) =>
        {
            run.Batches.Enqueue($"{partition.Partition.Value}:{string.Join(",", batch)}");
            if (partition.Partition.Value == 0) { processing.TrySetResult(); await release.Task; }
        }, handoff);
        await processing.Task.WaitAsync(Wait);
        handoff.Request();
        old.Records.Enqueue(FakeConsumer.Record(5, partition: 1));
        await Until(() => old.Records.IsEmpty && old.Paused.ContainsKey(partition1));
        Assert.That(run.Batches, Is.EqualTo(new[] { "0:10" }), "Nothing new is dispatched while draining.");
        release.TrySetResult();
        await Until(() => run.Batches.Contains("1:5") && old.Events.Any(e => e.StartsWith("old commit") && e.Contains("1:6")));
        run.Stop.Cancel();
        await consuming.WaitAsync(Wait);
        Assert.That(handoff.Handoffs, Is.EqualTo(1));
    }
}
