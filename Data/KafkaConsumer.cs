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
    public partial class KafkaConsumer
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
        public static Task Consume<T>(IConfiguration config, string topic, Func<T, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            // rebuilt whenever it ends without cancellation, after a 2 s..60 s backoff (KafkaConsumer.Retry.cs)
            return ConsumeRetrying<T>(
                (handler, token) => ConsumeBatch(config, topic, handler, token, groupId, 1, start, deserializer),
                topic, action, cancleToken, Task.Delay);
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
            var consumerConfig = new ConsumerConfig(KafkaCreator.GetClientConfig(config))
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
            };
            if (KafkaRenewalFlag.IsOn(config["KAFKA:TLS:RELOAD_ON_CHANGE"]))
                await ConsumeBatchRenewing(config, consumerConfig, topics, action, cancleToken, maxChunkSize, deserializer);
            else
                await ConsumeBatchPlain(consumerConfig, topics, action, cancleToken, maxChunkSize, deserializer);
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

        /// <summary>
        /// Consume batches with the given settings. As no IConfiguration is passed, <c>KAFKA:TLS:RELOAD_ON_CHANGE</c> is read
        /// from <see cref="SimplerConfig.SConfig"/> (appsettings, custom.conf.json, environment); renewal uses this config's connection.
        /// </summary>
        public static Task ConsumeBatch<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<IEnumerable<T>, Task> action,
                                                CancellationToken cancleToken,
                                                int maxChunkSize = 500,
                                                IDeserializer<T> deserializer = null)
        {
            if (AmbientRenewalConfiguration(config) is { } renewal)
                return ConsumeBatchRenewing(renewal, config, topics, action, cancleToken, maxChunkSize, deserializer);
            return ConsumeBatchPlain(config, topics, action, cancleToken, maxChunkSize, deserializer);
        }

        private static Task ConsumeBatchPlain<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<IEnumerable<T>, Task> action,
                                                CancellationToken cancleToken,
                                                int maxChunkSize,
                                                IDeserializer<T> deserializer)
        {
            if (deserializer == null)
                deserializer = SerializerFactory.GetDeserializer<T>();
            return ConsumeBatchPlain(config, topics, action, cancleToken, maxChunkSize,
                settings => new ConsumerBuilder<Ignore, T>(settings).SetValueDeserializer(deserializer).Build());
        }

        // build: creates the native consumer; a throw from it or from Subscribe faults the returned task
        internal static async Task ConsumeBatchPlain<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<IEnumerable<T>, Task> action,
                                                CancellationToken cancleToken,
                                                int maxChunkSize,
                                                Func<ConsumerConfig, IConsumer<Ignore, T>> build)
        {
            var batch = new Queue<ConsumeResult<Ignore, T>>();
            var conf = new ConsumerConfig(config)
            {
                AutoCommitIntervalMs = 0
            };
            // in case this method is awaited on in a backgroundWorker
            await Task.Yield();

            // wrap in long running thread; Unwrap so a failing build/subscribe reaches the caller instead of hanging
            var consuming = Task.Factory.StartNew(
                () => ConsumeBatchThread<Ignore,T>(config, topics, (message)=>action(message.Select(m=>m.Value)), maxChunkSize, build, batch, conf, cancleToken),
                TaskCreationOptions.LongRunning).Unwrap();
            Console.WriteLine($"Started consumer for {string.Join(',', topics)}");
            await consuming;
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
            Func<ConsumerConfig, PartitionHandlers, IConsumer<Ignore, T>> createConsumer =
                (settings, handlers) => BuildPartitionedConsumer(settings, deserializer, handlers);
            var createRenewal = PartitionedRenewal<T>(AmbientRenewalConfiguration(conf), conf, topics);
            return Task.Factory.StartNew(
                () => ConsumePartitionedParallelBatchThread(conf, topics, action, maxChunkSizePerPartition, partitionsRevoked, partitionStallTimeout, createConsumer, createRenewal, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        // createRenewal: null on the plain path, else builds the TLS renewal over the given consumer factory
        internal static void ConsumePartitionedParallelBatchThread<T>(
                                                ConsumerConfig config,
                                                string[] topics,
                                                Func<TopicPartition, IEnumerable<T>, Task> action,
                                                int maxChunkSizePerPartition,
                                                Action<IEnumerable<TopicPartition>> partitionsRevoked,
                                                TimeSpan? partitionStallTimeout,
                                                Func<ConsumerConfig, PartitionHandlers, IConsumer<Ignore, T>> createConsumer,
                                                Func<Func<ConsumerConfig, Coflnet.Kafka.KafkaConsumerReadiness, IConsumer<Ignore, T>>, IConsumerHandoff<T>> createRenewal,
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
            // resumed from the main loop: the assignment is only applied after this handler returns
            var resumeAfterAssign = new List<TopicPartition>();
            IConsumer<Ignore, T> consumer = null;
            void Assigned(List<TopicPartition> assigned)
            {
                var now = DateTime.UtcNow;
                resumeAfterAssign.AddRange(PartitionLagPolicy.ResumeOnAssign(assigned, partition => inFlight.ContainsKey(partition)));
                foreach (var partition in assigned)
                {
                    partitionProgress[partition] = (Offset.Unset, now);
                    partitionLag.WithLabels(partition.Partition.Value.ToString()).Set(0);
                }
            }
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
            // readiness is null on the plain path; with renewal every handoff builds a new handle with these handlers
            IConsumer<Ignore, T> Build(ConsumerConfig settings, Coflnet.Kafka.KafkaConsumerReadiness readiness) =>
                createConsumer(settings, new PartitionHandlers(Assigned, RemovePartitions, readiness));
            void Buffer(ConsumeResult<Ignore, T> consumed)
            {
                if (!pending.TryGetValue(consumed.TopicPartition, out var partitionQueue))
                {
                    partitionQueue = new Queue<ConsumeResult<Ignore, T>>();
                    pending[consumed.TopicPartition] = partitionQueue;
                }
                partitionQueue.Enqueue(consumed);
            }
            var renewal = createRenewal?.Invoke(Build);
            consumer = StartPartitioned(renewal, config, topics, Build, Buffer, cancellationToken);
            var restartForRenewal = false;
            bool HandoffDue() => renewal != null && renewal.HandoffRequested.IsCancellationRequested;

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
                // draining for a TLS handoff: no new batch and no further fetching until HandOff
                if (HandoffDue())
                {
                    consumer.Pause(new[] { partition });
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

            void DispatchWaiting()
            {
                foreach (var partition in pending.Keys.Where(partition => !inFlight.ContainsKey(partition)).ToList())
                    DispatchOrResume(partition);
            }

            // safe boundary: nothing in flight. A failed forced commit does not hold the handoff back (the old
            // certificate may be expiring): a replacement then replays those batches, i.e. at-least-once duplicates.
            void HandOff()
            {
                CommitDue(force: true);
                var current = consumer;
                consumer = null; // owned by the renewal until it returns the handle to continue with
                var buffered = new List<ConsumeResult<Ignore, T>>();
                try
                {
                    consumer = renewal.HandoffAsync(current, buffered.Add, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception error) when (!cancellationToken.IsCancellationRequested)
                {
                    restartForRenewal = true;
                    dev.Logger.Instance.Error($"Kafka TLS handoff for {string.Join(',', topics)} failed ({error.GetType().Name}); restarting consumer host");
                    throw;
                }
                if (!ReferenceEquals(consumer, current))
                {
                    // records fetched by the closed handle are replayed from the committed offsets
                    pending.Clear();
                    pendingCommits.Clear();
                }
                buffered.ForEach(Buffer);
                foreach (var partition in consumer.Assignment.ToList())
                    DispatchOrResume(partition);
            }

            void Shutdown()
            {
                if (consumer == null)
                    return;
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

            try
            {
                DispatchWaiting(); // records the first handle fetched while it was being assigned
                while (!cancellationToken.IsCancellationRequested)
                {
                    FinishCompleted();
                    CommitDue();
                    if (HandoffDue() && inFlight.Count == 0)
                    {
                        HandOff();
                        continue;
                    }
                    ThrowIfPartitionStalled();
                    var consumed = consumer.Consume(TimeSpan.FromMilliseconds(50));
                    ResumeAssigned();
                    if (consumed == null)
                        continue;

                    var pullLimit = maxChunkSizePerPartition * Math.Max(1, consumer.Assignment.Count);
                    var pulled = 0;
                    do
                    {
                        Buffer(consumed);
                        pulled++;
                        consumed = pulled < pullLimit ? consumer.Consume(TimeSpan.Zero) : null;
                    }
                    while (consumed != null);

                    DispatchWaiting();
                }

                while (inFlight.Count > 0)
                {
                    consumer.Consume(TimeSpan.FromMilliseconds(50));
                    FinishCompleted();
                }
            }
            catch (OperationCanceledException) when (!restartForRenewal)
            {
                dev.Logger.Instance.Info($"Partitioned consumer for {string.Join(',', topics)} canceled");
            }
            catch (Exception) when (restartForRenewal)
            {
                throw; // logged by HandOff with its type only
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
                    Shutdown();
                }
                finally
                {
                    consumer?.Dispose();
                    DisposeRenewal(renewal);
                }
            }
        }

        private static async Task ConsumeBatchThread<TKey,TVal>(ConsumerConfig config, string[] topics, Func<IEnumerable<Message<Ignore, TVal>>, Task> action, int maxChunkSize, Func<ConsumerConfig, IConsumer<Ignore, TVal>> build, Queue<ConsumeResult<Ignore, TVal>> batch, ConsumerConfig conf, CancellationToken cancleToken)
        {
            var currentChunkSize = 1;
            using var c = build(conf);
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
