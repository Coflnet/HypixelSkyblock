using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Kafka;

// The renewal operations SkyCore's consume loops use (Coflnet.Core KafkaConsumerRenewal); lets tests drive handoffs without a broker.
internal interface IConsumerHandoff<T> : IAsyncDisposable
{
    CancellationToken HandoffRequested { get; }
    Task<IConsumer<Ignore, T>> StartAsync(Action<ConsumeResult<Ignore, T>> buffered, CancellationToken stoppingToken);
    Task<IConsumer<Ignore, T>> HandoffAsync(IConsumer<Ignore, T> current, Action<ConsumeResult<Ignore, T>> buffered, CancellationToken stoppingToken);
}

internal sealed class CoreConsumerHandoff<T>(KafkaConsumerRenewal<Ignore, T> renewal) : IConsumerHandoff<T>
{
    // null: the flag is off or the ConsumerConfig connects elsewhere than KAFKA:* (Coflnet.Core warns once); consume plainly then
    internal static async Task<IConsumerHandoff<T>> CreateAsync(IConfiguration settings, ConsumerConfig config, string[] topics,
        Func<ConsumerConfig, KafkaConsumerReadiness, IConsumer<Ignore, T>> build, ILogger logger)
    {
        var renewal = await KafkaConsumerRenewal<Ignore, T>.CreateAsync(settings, config, topics, build, logger);
        return renewal == null ? null : new CoreConsumerHandoff<T>(renewal);
    }

    public CancellationToken HandoffRequested => renewal.HandoffRequested;

    public Task<IConsumer<Ignore, T>> StartAsync(Action<ConsumeResult<Ignore, T>> buffered, CancellationToken stoppingToken) =>
        renewal.StartAsync(buffered, stoppingToken);

    public Task<IConsumer<Ignore, T>> HandoffAsync(IConsumer<Ignore, T> current, Action<ConsumeResult<Ignore, T>> buffered,
        CancellationToken stoppingToken) => renewal.HandoffAsync(current, buffered, stoppingToken);

    public ValueTask DisposeAsync() => renewal.DisposeAsync();
}

// The handlers one partitioned consumer handle reports to; Readiness is null on the plain path.
internal sealed record PartitionHandlers(
    Action<List<TopicPartition>> Assigned,
    Action<IEnumerable<TopicPartition>> Removed,
    KafkaConsumerReadiness Readiness);
