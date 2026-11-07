using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Kafka;

// Retry loop of Consume<T>: every end of the inner batch consumer that the caller did not ask for (exception or
// normal return) rebuilds it after a ConsumeBackoff delay, so a poison message or a failing renewal cannot hot-loop.
public partial class KafkaConsumer
{
    // clock: null uses the system clock; tests pass their own to drive the stable-generation reset
    internal static async Task ConsumeRetrying<T>(Func<Func<IEnumerable<T>, Task>, CancellationToken, Task> consumeBatch,
        string topic, Func<T, Task> action, CancellationToken stoppingToken, Func<TimeSpan, CancellationToken, Task> delay,
        TimeProvider clock = null)
    {
        var backoff = new ConsumeBackoff(clock ?? TimeProvider.System);
        Func<IEnumerable<T>, Task> handler = async batch =>
        {
            foreach (var message in batch)
                await action(message);
            backoff.MarkProcessed();
        };
        while (!stoppingToken.IsCancellationRequested)
        {
            backoff.GenerationStarted();
            var failure = await RunConsumer(consumeBatch, handler, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
            {
                LogFailureAfterStop(topic, failure);
                return;
            }
            var wait = backoff.NextDelay();
            LogConsumerEnded(topic, failure, backoff.Attempt, wait);
            if (!await WaitBeforeRetry(delay, wait, stoppingToken))
                return;
        }
    }

    // The exception the inner consumer ended with, null for a normal return or a cancellation by the caller.
    private static async Task<Exception> RunConsumer<T>(Func<Func<IEnumerable<T>, Task>, CancellationToken, Task> consumeBatch,
        Func<IEnumerable<T>, Task> handler, CancellationToken stoppingToken)
    {
        try
        {
            await consumeBatch(handler, stoppingToken);
            return null;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    private static void LogConsumerEnded(string topic, Exception failure, int attempt, TimeSpan wait)
    {
        processFail.Inc();
        var retry = $"(attempt {attempt}), retrying in {wait.TotalSeconds:0}s";
        if (failure != null)
        {
            dev.Logger.Instance.Error(failure, $"Kafka consumer process for {topic} failed {retry}");
            return;
        }
        renewalLog.Value.LogWarning("Kafka consumer process for {Topic} ended without cancellation {Retry}", topic, retry);
    }

    // The caller stopped us, so this is no retry case, but the failure is not dropped silently either.
    private static void LogFailureAfterStop(string topic, Exception failure)
    {
        if (failure == null)
            return;
        renewalLog.Value.LogInformation(failure, "Kafka consumer process for {Topic} failed while stopping", topic);
    }

    // False when the caller cancelled during the wait.
    private static async Task<bool> WaitBeforeRetry(Func<TimeSpan, CancellationToken, Task> delay, TimeSpan wait,
        CancellationToken stoppingToken)
    {
        try
        {
            await delay(wait, stoppingToken);
            return !stoppingToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
