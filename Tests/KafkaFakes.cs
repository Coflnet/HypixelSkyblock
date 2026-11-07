using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Kafka;
using Confluent.Kafka;

namespace Coflnet.Sky.Core.Tests;

// In-memory IConsumer for the consume loops: no librdkafka handle, no broker.
public class FakeConsumer : DispatchProxy
{
    public ConcurrentQueue<ConsumeResult<Ignore, string>> Records { get; } = new();
    public ConcurrentQueue<string> Events { get; private set; } = new();
    public List<TopicPartition> Assignment { get; set; } = new();
    public ConcurrentDictionary<TopicPartition, bool> Paused { get; } = new();
    public Action OnSubscribe { get; set; }
    public Action OnConsume { get; set; }
    public Action OnClose { get; set; }
    public string Name { get; private set; }
    public int Closes { get; private set; }
    public int Disposals { get; private set; }

    public IConsumer<Ignore, string> Consumer => (IConsumer<Ignore, string>)(object)this;

    public static FakeConsumer Create(string name, ConcurrentQueue<string> events)
    {
        var fake = (FakeConsumer)(object)Create<IConsumer<Ignore, string>, FakeConsumer>();
        fake.Name = name;
        fake.Events = events;
        return fake;
    }

    public static ConsumeResult<Ignore, string> Record(long offset, int partition = 0) =>
        new() { Topic = "renewal-test", Partition = partition, Offset = offset, Message = new() { Value = offset.ToString() } };

    protected override object Invoke(MethodInfo method, object[] args)
    {
        switch (method.Name)
        {
            case "Consume":
                OnConsume?.Invoke();
                return args[0] is CancellationToken token ? ConsumeBlocking(token) : ConsumeWithin(args[0]);
            case "Commit" when args.Length == 1 && args[0] is IEnumerable<TopicPartitionOffset> offsets:
                Events.Enqueue($"{Name} commit " + string.Join(",", offsets.Select(o => $"{o.Partition.Value}:{o.Offset.Value}")));
                return null;
            case "Subscribe":
                OnSubscribe?.Invoke();
                return null;
            case "Close":
                Closes++;
                Events.Enqueue($"{Name} close");
                OnClose?.Invoke();
                return null;
            case "Dispose":
                Disposals++;
                return null;
            case "Pause":
                foreach (var partition in (IEnumerable<TopicPartition>)args[0]) Paused[partition] = true;
                return null;
            case "Resume":
                foreach (var partition in (IEnumerable<TopicPartition>)args[0]) Paused.TryRemove(partition, out _);
                return null;
            case "get_Assignment":
                return Assignment.ToList();
            case "GetWatermarkOffsets":
                return new WatermarkOffsets(0, 100);
            case "Position":
                return new Offset(50);
            default:
                return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }

    private ConsumeResult<Ignore, string> ConsumeBlocking(CancellationToken token)
    {
        while (true)
        {
            if (Records.TryDequeue(out var record))
                return record;
            token.ThrowIfCancellationRequested();
            Thread.Sleep(2);
        }
    }

    private ConsumeResult<Ignore, string> ConsumeWithin(object timeout)
    {
        if (Records.TryDequeue(out var record))
            return record;
        var wait = timeout is TimeSpan span ? span : TimeSpan.FromMilliseconds((int)timeout);
        if (wait > TimeSpan.Zero)
            Thread.Sleep(TimeSpan.FromMilliseconds(Math.Min(2, wait.TotalMilliseconds)));
        return null;
    }
}

// Drives handoffs by hand; Start/Handoff decide which handle the loop continues with.
internal sealed class FakeHandoff : IConsumerHandoff<string>
{
    private CancellationTokenSource requested = new();

    public Func<IConsumer<Ignore, string>> Start { get; set; }
    // records the first handle fetched while it was being assigned
    public List<ConsumeResult<Ignore, string>> StartBuffered { get; } = new();
    public Func<IConsumer<Ignore, string>, Action<ConsumeResult<Ignore, string>>, IConsumer<Ignore, string>> Handoff { get; set; }
    public int Handoffs;
    public bool Disposed { get; private set; }

    public void Request() => requested.Cancel();

    public CancellationToken HandoffRequested => requested.Token;

    public Task<IConsumer<Ignore, string>> StartAsync(Action<ConsumeResult<Ignore, string>> buffered, CancellationToken stoppingToken)
    {
        var consumer = Start();
        StartBuffered.ForEach(buffered);
        return Task.FromResult(consumer);
    }

    public Task<IConsumer<Ignore, string>> HandoffAsync(IConsumer<Ignore, string> current,
        Action<ConsumeResult<Ignore, string>> buffered, CancellationToken stoppingToken)
    {
        Interlocked.Increment(ref Handoffs);
        requested = new CancellationTokenSource();
        return Task.FromResult(Handoff(current, buffered));
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return default;
    }
}
