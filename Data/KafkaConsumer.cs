using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Prometheus;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Coflnet.Sky.Kafka
{
    public class KafkaConsumer
    {
        static Counter processFail = Metrics.CreateCounter("consume_process_failed", "How often processing of consumed messages failed");
        static ConcurrentDictionary<string, Gauge> consumerOffsets = new();
        static ConcurrentDictionary<string, Gauge> partitionConsumerOffsets = new();
        /// <summary>
        /// Generic consumer
        /// </summary>
        /// <param name="config"></param>
        /// <param name="topic"></param>
        /// <param name="action"></param>
        /// <param name="cancleToken"></param>
        /// <param name="groupId"></param>
        /// <param name="start">What event to start at</param>
        /// <param name="deserializer">The deserializer used for new messages</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task Consume<T>(IConfiguration config, string topic, Func<T, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            while (!cancleToken.IsCancellationRequested)
                try
                {
                    await ConsumeBatch(config, topic, async batch =>
                    {
                        foreach (var message in batch)
                            await action(message);
                    }, cancleToken, groupId, 1, start, deserializer);
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, $"Kafka consumer process for {topic}");
                    processFail.Inc();
                    // A consumer that keeps failing (bad message, downstream outage, ...) would
                    // otherwise hot-loop here with no delay, hammering logs/metrics and spinning
                    // on CPU while offering no chance for a transient failure to clear. Back off
                    // instead of retrying immediately.
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancleToken);
                    }
                    catch (OperationCanceledException) when (cancleToken.IsCancellationRequested)
                    {
                        // shutting down; let the outer while condition end the loop cleanly
                    }
                }
        }


        /// <summary>
        /// Consume a batch of messages for a single topic
        /// </summary>
        /// <param name="config"></param>
        /// <param name="topic"></param>
        /// <param name="action"></param>
        /// <param name="cancleToken"></param>
        /// <param name="groupId"></param>
        /// <param name="maxChunkSize"></param>
        /// <param name="start"></param>
        /// <param name="deserializer"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Task ConsumeBatch<T>(IConfiguration config, string topic, Func<IEnumerable<T>, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            int maxChunkSize = 500,
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            return ConsumeBatch<T>(config, new string[] { topic }, action, cancleToken, groupId, maxChunkSize, start, deserializer);
        }

        /// <summary>
        /// Consume a batch of messages for multiple topics 
        /// </summary>
        /// <param name="config"></param>
        /// <param name="topics"></param>
        /// <param name="action"></param>
        /// <param name="cancleToken"></param>
        /// <param name="groupId"></param>
        /// <param name="maxChunkSize"></param>
        /// <param name="start"></param>
        /// <param name="deserializer"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task ConsumeBatch<T>(IConfiguration config, string[] topics, Func<IEnumerable<T>, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            int maxChunkSize = 500,
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            await ConsumeBatch(new ConsumerConfig(KafkaCreator.GetClientConfig(config))
            {
                GroupId = groupId,

                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = start,
                EnableAutoCommit = false,
                PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
            }, topics, action, cancleToken, maxChunkSize, deserializer);
        }
        public static async Task ConsumeBatch<T>(
                                                    ConsumerConfig config,
                                                    string topic,
                                                    Func<IEnumerable<T>, Task> action,
                                                    CancellationToken cancleToken,
                                                    int maxChunkSize = 500,
                                                    IDeserializer<T> deserializer = null)
        {
            await ConsumeBatch(config, new string[] { topic }, action, cancleToken, maxChunkSize, deserializer);
        }

        public static async Task ConsumeBatch<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<IEnumerable<T>, Task> action,
                                                CancellationToken cancleToken,
                                                int maxChunkSize = 500,
                                                IDeserializer<T> deserializer = null)
        {
            var batch = new Queue<ConsumeResult<Ignore, T>>();
            var conf = new ConsumerConfig(config)
            {
                AutoCommitIntervalMs = 0
            };
            // in case this method is awaited on in a backgroundWorker
            await Task.Yield();

            if (deserializer == null)
                deserializer = SerializerFactory.GetDeserializer<T>();
            // wrap in long running thread
            var completion = new TaskCompletionSource<bool>();
            await Task.Factory.StartNew(async () =>
            {
                await ConsumeBatchThread<Ignore,T>(config, topics, (message)=>action(message.Select(m=>m.Value)), maxChunkSize, deserializer, batch, conf, cancleToken);
                completion.SetResult(true);
            }, TaskCreationOptions.LongRunning);
            Console.WriteLine($"Started consumer for {string.Join(',', topics)}");
            await completion.Task;
        }

        /// <summary>
        /// Consumes and commits each Kafka partition independently. Records stay ordered within
        /// their partition while different partitions can be processed concurrently.
        /// </summary>
        public static Task ConsumePartitionedParallelBatch<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<TopicPartition, IEnumerable<T>, Task> action,
                                                CancellationToken cancellationToken,
                                                int maxChunkSizePerPartition = 500,
                                                IDeserializer<T> deserializer = null,
                                                Action<IEnumerable<TopicPartition>> partitionsRevoked = null,
                                                TimeSpan? partitionStallTimeout = null)
        {
            if (maxChunkSizePerPartition < 1)
                throw new ArgumentOutOfRangeException(nameof(maxChunkSizePerPartition));
            if (partitionStallTimeout.HasValue && partitionStallTimeout.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(partitionStallTimeout));
            var conf = new ConsumerConfig(config)
            {
                EnableAutoCommit = false
            };
            deserializer ??= SerializerFactory.GetDeserializer<T>();
            return Task.Factory.StartNew(
                () => ConsumePartitionedParallelBatchThread(conf, topics, action, maxChunkSizePerPartition, deserializer, partitionsRevoked, partitionStallTimeout, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private static void ConsumePartitionedParallelBatchThread<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<TopicPartition, IEnumerable<T>, Task> action,
                                                int maxChunkSizePerPartition,
                                                IDeserializer<T> deserializer,
                                                Action<IEnumerable<TopicPartition>> partitionsRevoked,
                                                TimeSpan? partitionStallTimeout,
                                                CancellationToken cancellationToken)
        {
            var pending = new Dictionary<TopicPartition, Queue<ConsumeResult<Ignore, T>>>();
            var inFlight = new Dictionary<TopicPartition, (Task Task, List<ConsumeResult<Ignore, T>> Batch)>();
            var partitionProgress = new Dictionary<TopicPartition, (Offset Offset, DateTime At)>();
            // offsets of fully processed batches whose commit has not succeeded yet (transient
            // broker errors like "Not coordinator"); retried instead of dropped
            var pendingCommits = new Dictionary<TopicPartition, (Offset Next, DateTime LastCommitOrStart, DateTime NextTry)>();
            var restartForStall = false;
            var nextStallCheck = DateTime.UtcNow;
            var partitionMetricKey = "kafka_partition_lag_" + string.Join('_', topics.Select(
                topic => System.Text.RegularExpressions.Regex.Replace(topic, "[^a-zA-Z0-9]", "_")));
            var partitionLag = partitionConsumerOffsets.GetOrAdd(partitionMetricKey, key => Metrics.CreateGauge(
                key,
                "Kafka consumer offset lag by partition",
                new GaugeConfiguration { LabelNames = new[] { "partition" } }));
            var builder = new ConsumerBuilder<Ignore, T>(config).SetValueDeserializer(deserializer);
            // resumed from the main loop: the assignment is only applied after this handler returns
            var resumeAfterAssign = new List<TopicPartition>();
            builder.SetPartitionsAssignedHandler((_, assigned) =>
            {
                var now = DateTime.UtcNow;
                resumeAfterAssign.AddRange(PartitionLagPolicy.ResumeOnAssign(assigned, partition => inFlight.ContainsKey(partition)));
                foreach (var partition in assigned)
                {
                    partitionProgress[partition] = (Offset.Unset, now);
                    partitionLag.WithLabels(partition.Partition.Value.ToString()).Set(0);
                }
            });
            void RemovePartitions(IEnumerable<TopicPartition> partitions)
            {
                var removed = partitions.ToList();
                foreach (var partition in removed)
                {
                    pending.Remove(partition);
                    partitionProgress.Remove(partition);
                    pendingCommits.Remove(partition);
                    partitionLag.RemoveLabelled(partition.Partition.Value.ToString());
                }
                partitionsRevoked?.Invoke(removed);
            }
            builder.SetPartitionsRevokedHandler((_, revoked) =>
                RemovePartitions(revoked.Select(offset => offset.TopicPartition)));
            builder.SetPartitionsLostHandler((_, lost) =>
                RemovePartitions(lost.Select(offset => offset.TopicPartition)));
            using var consumer = builder.Build();
            consumer.Subscribe(topics);

            void ThrowIfPartitionStalled()
            {
                var now = DateTime.UtcNow;
                if (!partitionStallTimeout.HasValue || now < nextStallCheck)
                    return;
                nextStallCheck = now.AddSeconds(5);
                foreach (var partition in consumer.Assignment)
                {
                    if (!partitionProgress.TryGetValue(partition, out var progress))
                        partitionProgress[partition] = progress = (Offset.Unset, now);
                    if (progress.Offset == Offset.Unset)
                    {
                        Offset committed;
                        try
                        {
                            committed = consumer.Committed(new[] { partition }, TimeSpan.FromSeconds(2)).Single().Offset;
                        }
                        catch (KafkaException error)
                        {
                            dev.Logger.Instance.Error(error, $"Could not inspect committed offset for {partition}");
                            continue;
                        }
                        partitionProgress[partition] = progress = (committed, progress.At);
                    }
                    var watermarks = consumer.GetWatermarkOffsets(partition);
                    var processedThrough = progress.Offset == Offset.Unset ? watermarks.Low : progress.Offset;
                    var highWatermark = watermarks.High;
                    // the cache is stale for partitions that are not fetching (e.g. paused), ask the broker
                    var queriedLag = PartitionLagPolicy.Lag(watermarks.High.Value, processedThrough.Value, () =>
                    {
                        try
                        {
                            highWatermark = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(2)).High;
                            return highWatermark.Value;
                        }
                        catch (KafkaException error)
                        {
                            dev.Logger.Instance.Error(error, $"Could not query watermarks for {partition}");
                            return null;
                        }
                    });
                    if (queriedLag == null)
                        continue;
                    var lag = queriedLag.Value;
                    partitionLag.WithLabels(partition.Partition.Value.ToString()).Set(lag);
                    if (lag == 0)
                    {
                        partitionProgress[partition] = (progress.Offset, now);
                        continue;
                    }
                    var commitFailingSince = pendingCommits.TryGetValue(partition, out var waiting) ? waiting.LastCommitOrStart : (DateTime?)null;
                    if (!PartitionStallPolicy.IsStalled(now, progress.At, commitFailingSince, partitionStallTimeout.Value))
                        continue;
                    restartForStall = true;
                    var idleStall = now - progress.At > partitionStallTimeout.Value;
                    throw new TimeoutException(
                        (idleStall
                            ? $"Kafka partition {partition} has not committed or finished a batch for {now - progress.At:g} "
                            : $"Kafka partition {partition} commits kept failing for {now - commitFailingSince!.Value:g} while batches completed ") +
                        $"while lagging (committed {processedThrough}, high watermark {highWatermark}); restarting consumer host");
                }
            }

            void DispatchOrResume(TopicPartition partition)
            {
                if (!consumer.Assignment.Contains(partition))
                {
                    pending.Remove(partition);
                    // the pause flag outlives the assignment and would block a later reassignment
                    try
                    {
                        consumer.Resume(new[] { partition });
                    }
                    catch (KafkaException error)
                    {
                        dev.Logger.Instance.Info($"Could not resume unassigned {partition}: {error.Error.Reason}");
                    }
                    return;
                }
                if (pending.TryGetValue(partition, out var queue) && queue.Count > 0)
                {
                    consumer.Pause(new[] { partition });
                    var partitionBatch = new List<ConsumeResult<Ignore, T>>(Math.Min(queue.Count, maxChunkSizePerPartition));
                    while (partitionBatch.Count < maxChunkSizePerPartition && queue.TryDequeue(out var message))
                        partitionBatch.Add(message);
                    inFlight[partition] = (
                        Task.Run(() => action(partition, partitionBatch.Select(message => message.Message.Value))),
                        partitionBatch);
                    return;
                }
                pending.Remove(partition);
                consumer.Resume(new[] { partition });
            }

            void ResumeAssigned()
            {
                if (resumeAfterAssign.Count == 0)
                    return;
                var toResume = resumeAfterAssign.Where(partition => !inFlight.ContainsKey(partition) && consumer.Assignment.Contains(partition)).ToList();
                resumeAfterAssign.Clear();
                if (toResume.Count == 0)
                    return;
                try
                {
                    consumer.Resume(toResume);
                }
                catch (KafkaException error)
                {
                    dev.Logger.Instance.Error(error, $"Could not resume newly assigned {string.Join(',', toResume)}");
                }
            }

            // commits all pending offsets that are due (or all when force) in one call. Failures keep
            // the offsets pending and retry after a second; a failed commit only means reprocessing.
            // Note: a batch still in flight across a revoke+reassign may commit a stale offset, a
            // pre-existing limitation that is out of scope here.
            void CommitDue(bool force = false)
            {
                var now = DateTime.UtcNow;
                var due = new List<TopicPartition>();
                foreach (var entry in pendingCommits.ToList())
                {
                    if (!consumer.Assignment.Contains(entry.Key))
                        pendingCommits.Remove(entry.Key);
                    else if (force || entry.Value.NextTry <= now)
                        due.Add(entry.Key);
                }
                if (due.Count == 0)
                    return;
                try
                {
                    consumer.Commit(due.Select(partition => new TopicPartitionOffset(partition, pendingCommits[partition].Next)).ToList());
                    foreach (var partition in due)
                    {
                        partitionProgress[partition] = (pendingCommits[partition].Next, DateTime.UtcNow);
                        pendingCommits.Remove(partition);
                    }
                }
                catch (KafkaException error)
                {
                    foreach (var partition in due)
                    {
                        var waiting = pendingCommits[partition];
                        pendingCommits[partition] = (waiting.Next, waiting.LastCommitOrStart, now.AddSeconds(1));
                    }
                    dev.Logger.Instance.Error(error, $"On partition commit {string.Join(',', due)} {error.Error.IsFatal}, will retry");
                }
            }

            void FinishCompleted()
            {
                foreach (var completed in inFlight.Where(worker => worker.Value.Task.IsCompleted).ToList())
                {
                    var partition = completed.Key;
                    inFlight.Remove(partition);
                    try
                    {
                        completed.Value.Task.GetAwaiter().GetResult();
                    }
                    catch (Exception error)
                    {
                        processFail.Inc();
                        dev.Logger.Instance.Error(error, $"Kafka consumer process for {partition}");
                        pending.Remove(partition);
                        if (consumer.Assignment.Contains(partition))
                            consumer.Seek(completed.Value.Batch[0].TopicPartitionOffset);
                        DispatchOrResume(partition);
                        continue;
                    }
                    if (consumer.Assignment.Contains(partition))
                    {
                        var now = DateTime.UtcNow;
                        var nextOffset = completed.Value.Batch[^1].Offset + 1;
                        var since = pendingCommits.TryGetValue(partition, out var previous) ? previous.LastCommitOrStart : now;
                        var nextTry = pendingCommits.TryGetValue(partition, out var waitingCommit) ? waitingCommit.NextTry : now;
                        pendingCommits[partition] = (nextOffset, since, nextTry);
                        // processing finished a batch, so the partition is alive even if the commit fails
                        partitionProgress[partition] = (partitionProgress.TryGetValue(partition, out var known) ? known.Offset : Offset.Unset, now);
                    }
                    DispatchOrResume(partition);
                }
                CommitDue();
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    FinishCompleted();
                    CommitDue();
                    ThrowIfPartitionStalled();
                    var consumed = consumer.Consume(TimeSpan.FromMilliseconds(50));
                    ResumeAssigned();
                    if (consumed == null)
                        continue;

                    var pullLimit = maxChunkSizePerPartition * Math.Max(1, consumer.Assignment.Count);
                    var pulled = 0;
                    do
                    {
                        if (!pending.TryGetValue(consumed.TopicPartition, out var partitionQueue))
                        {
                            partitionQueue = new Queue<ConsumeResult<Ignore, T>>();
                            pending[consumed.TopicPartition] = partitionQueue;
                        }
                        partitionQueue.Enqueue(consumed);
                        pulled++;
                        consumed = pulled < pullLimit ? consumer.Consume(TimeSpan.Zero) : null;
                    }
                    while (consumed != null);

                    foreach (var partition in pending.Keys.Where(partition => !inFlight.ContainsKey(partition)).ToList())
                        DispatchOrResume(partition);
                }

                while (inFlight.Count > 0)
                {
                    consumer.Consume(TimeSpan.FromMilliseconds(50));
                    FinishCompleted();
                }
            }
            catch (OperationCanceledException)
            {
                dev.Logger.Instance.Info($"Partitioned consumer for {string.Join(',', topics)} canceled");
            }
            catch (Exception error)
            {
                dev.Logger.Instance.Error(error, $"Partitioned consumer for {string.Join(',', topics)}");
                if (restartForStall)
                    throw;
            }
            finally
            {
                try
                {
                    CommitDue(force: true);
                }
                catch (Exception)
                {
                    // failing here only means reprocessing
                }
                consumer.Close();
            }
        }

        private static async Task ConsumeBatchThread<TKey,TVal>(ConsumerConfig config, string[] topics, Func<IEnumerable<Message<Ignore, TVal>>, Task> action, int maxChunkSize, IDeserializer<TVal> deserializer, Queue<ConsumeResult<Ignore, TVal>> batch, ConsumerConfig conf, CancellationToken cancleToken)
        {
            var currentChunkSize = 1;
            using var c = new ConsumerBuilder<Ignore, TVal>(conf).SetValueDeserializer(deserializer).Build();
            c.Subscribe(topics);
            var key = "kafka_lag_" + string.Join('_', topics.Select(k => System.Text.RegularExpressions.Regex.Replace(k, "[^a-zA-Z0-9]", "_")));
            try
            {
                // reset all offsets
                while (!cancleToken.IsCancellationRequested)
                {
                    try
                    {
                        var extraLog = currentChunkSize < 2 && maxChunkSize > 2;
                        if (extraLog)
                            Console.WriteLine($"Polling for {currentChunkSize} messages from {string.Join(',', topics)}, config: {config.BootstrapServers}");
                        var cr = c.Consume(cancleToken);
                        batch.Enqueue(cr);
                        if (extraLog)
                            Console.WriteLine($"Consumed message '{cr.Message.Value}' at: '{cr.TopicPartitionOffset}'.");
                        while (batch.Count < currentChunkSize)
                        {
                            cr = c.Consume(TimeSpan.Zero);
                            if (cr == null)
                            {
                                break;
                            }
                            batch.Enqueue(cr);
                        }
                        await action(batch.Select(a => a.Message)).ConfigureAwait(false);
                        // tell kafka that we stored the batch
                        if (!config.EnableAutoCommit ?? true)
                            try
                            {
                                c.Commit(batch.Select(b => b.TopicPartitionOffset));
                                var lag = c.Assignment.Select(a => c.GetWatermarkOffsets(a).High - c.Position(a)).Sum();
                                consumerOffsets.GetOrAdd(key, Metrics.CreateGauge(key, "offset of kafka topic")).Set(lag);
                            }
                            catch (KafkaException e)
                            {
                                dev.Logger.Instance.Error(e, $"On commit {string.Join(',', topics)} {e.Error.IsFatal}");
                                dev.Logger.Instance.Info($"Assigned partitions: {string.Join(',', JsonConvert.SerializeObject(c.Assignment.Select(a => new { a.Topic, a.Partition, c.GetWatermarkOffsets(a).High, pos = c.Position(a) })))}");
                            }
                        batch.Clear();
                        if (currentChunkSize < maxChunkSize)
                            currentChunkSize++;
                    }
                    catch (ConsumeException e)
                    {
                        dev.Logger.Instance.Error(e, $"On consume {string.Join(',', topics)}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                dev.Logger.Instance.Info($"Consumer for {string.Join(',', topics)} canceled");
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"On consume {string.Join(',', topics)}");
            }
            finally
            {
                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                c.Close();
            }
        }
    }
}
