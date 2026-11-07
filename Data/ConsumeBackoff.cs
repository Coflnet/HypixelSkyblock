using System;
using System.Threading;

namespace Coflnet.Sky.Kafka;

// Delay between consumer rebuilds: 2 s doubling to 60 s. It goes back to 2 s only after a generation that processed a
// batch and stayed up for at least the max delay: the plain path commits each record's own offset, so every rebuild
// replays the last good record before a poison one, and resetting on any processed batch would rebuild every 2 s.
// Coflnet.Core's equivalent (KafkaConsumer.RebuildBackoff) is internal, hence this copy.
internal sealed class ConsumeBackoff(TimeProvider clock)
{
    internal static readonly TimeSpan Initial = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan Max = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan StableRun = Max;

    private TimeSpan next = Initial;
    private DateTimeOffset started;
    private int processed;

    // consecutive failures since the last stable generation, including the one NextDelay was called for
    public int Attempt { get; private set; }

    public void GenerationStarted()
    {
        Volatile.Write(ref processed, 0);
        started = clock.GetUtcNow();
    }

    // called from the consume thread after a batch was handled
    public void MarkProcessed() => Volatile.Write(ref processed, 1);

    public TimeSpan NextDelay()
    {
        if (WasStable())
            Reset();
        var delay = next;
        Attempt++;
        next = next * 2 < Max ? next * 2 : Max;
        return delay;
    }

    private bool WasStable()
    {
        var processedBatch = Interlocked.Exchange(ref processed, 0) == 1;
        return processedBatch && clock.GetUtcNow() - started >= StableRun;
    }

    private void Reset()
    {
        next = Initial;
        Attempt = 0;
    }
}
