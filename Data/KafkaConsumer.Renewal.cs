using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Kafka;
using Coflnet.Sky.Core;
using SerializerFactory = Coflnet.Sky.Core.SerializerFactory;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Coflnet.Sky.Kafka;

// Opt-in client certificate renewal (KAFKA:TLS:RELOAD_ON_CHANGE=true) through Coflnet.Core's KafkaConsumerRenewal.
// Auto-commit is off and the next offset of every processed partition is committed; a handoff only happens once the
// in-flight batch is processed and committed. Startup validation/preflight, the 60 s assignment wait and a failed
// post-Close recovery throw to the caller; everything else behaves like the plain path.
public partial class KafkaConsumer
{
    private static readonly Lazy<ILogger> renewalLog = new(() => dev.Logger.Factory.CreateLogger<KafkaConsumer>());

    // Overloads without an IConfiguration: only the opt-in comes from SimplerConfig; null keeps the plain path.
    internal static IConfiguration AmbientRenewalConfiguration(ConsumerConfig config) =>
        AmbientRenewalConfiguration(config, key => SimplerConfig.SConfig.Instance[key]);

    internal static IConfiguration AmbientRenewalConfiguration(ConsumerConfig config, Func<string, string> setting) =>
        AmbientOptIn(setting) ? RenewalConfiguration(config) : null;

    // Coflnet.Core reads the renewal baseline from KAFKA:*; build it from the caller's own connection and credentials.
    internal static IConfiguration RenewalConfiguration(ConsumerConfig config) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["KAFKA:BROKERS"] = config.BootstrapServers,
            ["KAFKA:USERNAME"] = config.SaslUsername,
            ["KAFKA:PASSWORD"] = config.SaslPassword,
            ["KAFKA:TLS:CA_LOCATION"] = config.SslCaLocation,
            ["KAFKA:TLS:CERTIFICATE_LOCATION"] = config.SslCertificateLocation,
            ["KAFKA:TLS:KEY_LOCATION"] = config.SslKeyLocation,
            ["KAFKA:TLS:RELOAD_ON_CHANGE"] = "true"
        })
        .Build();

    // An unreadable SimplerConfig (e.g. malformed custom.conf.json) must not change the default path.
    private static bool AmbientOptIn(Func<string, string> setting)
    {
        try
        {
            return KafkaRenewalFlag.IsOn(setting("KAFKA:TLS:RELOAD_ON_CHANGE"));
        }
        catch (Exception e)
        {
            dev.Logger.Instance.Error($"Could not read KAFKA:TLS:RELOAD_ON_CHANGE from SimplerConfig ({e.GetType().Name}); Kafka TLS renewal stays off");
            return false;
        }
    }

    private static async Task ConsumeBatchRenewing<T>(IConfiguration settings, ConsumerConfig config, string[] topics,
        Func<IEnumerable<T>, Task> action, CancellationToken stoppingToken, int maxChunkSize, IDeserializer<T> deserializer)
    {
        deserializer ??= SerializerFactory.GetDeserializer<T>();
        var conf = new ConsumerConfig(config.ToDictionary(item => item.Key, item => item.Value)) { AutoCommitIntervalMs = 0 };
        await using var renewal = await CoreConsumerHandoff<T>.CreateAsync(settings, conf, topics,
            (options, readiness) => readiness.Attach(new ConsumerBuilder<Ignore, T>(options).SetValueDeserializer(deserializer)).Build(),
            renewalLog.Value);
        if (renewal == null)
        {
            // connects elsewhere than KAFKA:* (Coflnet.Core warned once): no renewal for this consumer
            await ConsumeBatchPlain(config, topics, action, stoppingToken, maxChunkSize, deserializer);
            return;
        }
        await Task.Factory.StartNew(() => ConsumeRenewingBatches(renewal, topics, action, maxChunkSize, stoppingToken),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    internal static async Task ConsumeRenewingBatches<T>(IConsumerHandoff<T> renewal, string[] topics,
        Func<IEnumerable<T>, Task> action, int maxChunkSize, CancellationToken stoppingToken)
    {
        var batch = new Queue<ConsumeResult<Ignore, T>>();
        var consumer = await renewal.StartAsync(batch.Enqueue, stoppingToken);
        Console.WriteLine($"Started consumer for {string.Join(',', topics)}");
        try
        {
            var chunkSize = 1;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (batch.Count == 0 && renewal.HandoffRequested.IsCancellationRequested)
                {
                    var current = consumer;
                    consumer = null; // owned by the renewal until it returns the handle to continue with
                    consumer = await renewal.HandoffAsync(current, batch.Enqueue, stoppingToken);
                    continue;
                }
                if (!await ConsumeOneBatch(consumer, batch, chunkSize, action, topics, renewal.HandoffRequested, stoppingToken))
                    return;
                if (chunkSize < maxChunkSize)
                    chunkSize++;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            dev.Logger.Instance.Info($"Consumer for {string.Join(',', topics)} canceled");
        }
        finally
        {
            try { consumer?.Close(); }
            finally { consumer?.Dispose(); }
        }
    }

    // False when processing failed: logged and the consumer stops, exactly like the plain path.
    // Kafka/TLS failures log only their code or type (their text is not guaranteed secret-free).
    private static async Task<bool> ConsumeOneBatch<T>(IConsumer<Ignore, T> consumer, Queue<ConsumeResult<Ignore, T>> batch,
        int chunkSize, Func<IEnumerable<T>, Task> action, string[] topics, CancellationToken handoff, CancellationToken stoppingToken)
    {
        try
        {
            if (!Fill(consumer, batch, chunkSize, handoff, stoppingToken))
                return true;
            if (!await Process(action, batch, topics, stoppingToken))
                return false;
            CommitNextOffsets(consumer, batch, topics);
            batch.Clear();
            return true;
        }
        catch (ConsumeException e)
        {
            dev.Logger.Instance.Error($"Renewable Kafka consume failed ({e.Error.Code}) for {string.Join(',', topics)}");
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            dev.Logger.Instance.Error($"Renewable Kafka consumer for {string.Join(',', topics)} stopped ({e.GetType().Name})");
            return false;
        }
    }

    // Application failures keep the full exception, like the plain path: it is the caller's code, not Kafka/TLS.
    private static async Task<bool> Process<T>(Func<IEnumerable<T>, Task> action, Queue<ConsumeResult<Ignore, T>> batch,
        string[] topics, CancellationToken stoppingToken)
    {
        try
        {
            await action(batch.Select(record => record.Message.Value)).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            dev.Logger.Instance.Error(e, $"Kafka consumer process for {string.Join(',', topics)}");
            return false;
        }
    }

    // False when a due handoff interrupted the wait for the first record; a record returned meanwhile is kept.
    private static bool Fill<T>(IConsumer<Ignore, T> consumer, Queue<ConsumeResult<Ignore, T>> batch, int chunkSize,
        CancellationToken handoff, CancellationToken stoppingToken)
    {
        if (batch.Count == 0)
        {
            using var interrupt = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, handoff);
            try
            {
                batch.Enqueue(consumer.Consume(interrupt.Token));
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                return false;
            }
        }
        while (batch.Count < chunkSize && consumer.Consume(TimeSpan.Zero) is { } record)
            batch.Enqueue(record);
        return true;
    }

    // Next offset per processed partition: Close never acknowledges a fetched but unprocessed record.
    private static void CommitNextOffsets<T>(IConsumer<Ignore, T> consumer, Queue<ConsumeResult<Ignore, T>> batch, string[] topics)
    {
        try
        {
            consumer.Commit(batch.GroupBy(record => record.TopicPartition)
                .Select(partition => new TopicPartitionOffset(partition.Key, partition.Max(record => record.Offset.Value) + 1))
                .ToList());
            var lag = consumer.Assignment.Select(a => consumer.GetWatermarkOffsets(a).High - consumer.Position(a)).Sum();
            var key = LagMetricKey(topics);
            consumerOffsets.GetOrAdd(key, name => Metrics.CreateGauge(name, "offset of kafka topic")).Set(lag);
        }
        catch (KafkaException e)
        {
            dev.Logger.Instance.Error($"Renewable Kafka commit failed ({e.Error.Code}) for {string.Join(',', topics)}; unconfirmed offsets may replay");
        }
    }

    internal static string LagMetricKey(string[] topics) =>
        "kafka_lag_" + string.Join('_', topics.Select(k => System.Text.RegularExpressions.Regex.Replace(k, "[^a-zA-Z0-9]", "_")));

    // null keeps the partitioned loop on the plain path; the renewal is created on the loop's thread over its consumer factory.
    internal static Func<Func<ConsumerConfig, KafkaConsumerReadiness, IConsumer<Ignore, T>>, IConsumerHandoff<T>> PartitionedRenewal<T>(
        IConfiguration settings, ConsumerConfig config, string[] topics)
    {
        if (settings == null)
            return null;
        return build => CoreConsumerHandoff<T>.CreateAsync(settings, config, topics, build, renewalLog.Value).GetAwaiter().GetResult();
    }

    internal static IConsumer<Ignore, T> BuildPartitionedConsumer<T>(ConsumerConfig settings, IDeserializer<T> deserializer, PartitionHandlers handlers)
    {
        var builder = new ConsumerBuilder<Ignore, T>(settings).SetValueDeserializer(deserializer);
        builder.SetPartitionsAssignedHandler((_, assigned) =>
        {
            handlers.Assigned(assigned);
            handlers.Readiness?.PartitionsAssigned();
        });
        builder.SetPartitionsRevokedHandler((_, revoked) => handlers.Removed(revoked.Select(offset => offset.TopicPartition)));
        builder.SetPartitionsLostHandler((_, lost) => handlers.Removed(lost.Select(offset => offset.TopicPartition)));
        if (handlers.Readiness != null)
            builder.SetErrorHandler((_, error) => handlers.Readiness.ReportError(error));
        return builder.Build();
    }

    // The first handle of the partitioned loop: subscribed, and with renewal also assigned (records fetched meanwhile buffered).
    private static IConsumer<Ignore, T> StartPartitioned<T>(IConsumerHandoff<T> renewal, ConsumerConfig config,
        string[] topics, Func<ConsumerConfig, KafkaConsumerReadiness, IConsumer<Ignore, T>> build,
        Action<ConsumeResult<Ignore, T>> buffered, CancellationToken stoppingToken)
    {
        if (renewal != null)
            return StartRenewing(renewal, buffered, stoppingToken);
        var consumer = build(config, null);
        try
        {
            consumer.Subscribe(topics);
            return consumer;
        }
        catch
        {
            consumer.Dispose();
            throw;
        }
    }

    private static IConsumer<Ignore, T> StartRenewing<T>(IConsumerHandoff<T> renewal,
        Action<ConsumeResult<Ignore, T>> buffered, CancellationToken stoppingToken)
    {
        try
        {
            return renewal.StartAsync(buffered, stoppingToken).GetAwaiter().GetResult();
        }
        catch
        {
            DisposeRenewal(renewal);
            throw;
        }
    }

    private static void DisposeRenewal<T>(IConsumerHandoff<T> renewal) =>
        renewal?.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
