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
                                                Action<IEnumerable<TopicPartition>> partitionsRevoked = null)
        {
            if (maxChunkSizePerPartition < 1)
                throw new ArgumentOutOfRangeException(nameof(maxChunkSizePerPartition));
            var conf = new ConsumerConfig(config)
            {
                EnableAutoCommit = false
            };
            deserializer ??= SerializerFactory.GetDeserializer<T>();
            return Task.Factory.StartNew(
                () => ConsumePartitionedParallelBatchThread(conf, topics, action, maxChunkSizePerPartition, deserializer, partitionsRevoked, cancellationToken),
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
                                                CancellationToken cancellationToken)
        {
            var pending = new Dictionary<TopicPartition, Queue<ConsumeResult<Ignore, T>>>();
            var inFlight = new Dictionary<TopicPartition, (Task Task, List<ConsumeResult<Ignore, T>> Batch)>();
            var builder = new ConsumerBuilder<Ignore, T>(config).SetValueDeserializer(deserializer);
            void RemovePartitions(IEnumerable<TopicPartition> partitions)
            {
                var removed = partitions.ToList();
                foreach (var partition in removed)
                    pending.Remove(partition);
                partitionsRevoked?.Invoke(removed);
            }
            builder.SetPartitionsRevokedHandler((_, revoked) =>
                RemovePartitions(revoked.Select(offset => offset.TopicPartition)));
            builder.SetPartitionsLostHandler((_, lost) =>
                RemovePartitions(lost.Select(offset => offset.TopicPartition)));
            using var consumer = builder.Build();
            consumer.Subscribe(topics);
            var metricKey = "kafka_lag_" + string.Join('_', topics.Select(
                topic => System.Text.RegularExpressions.Regex.Replace(topic, "[^a-zA-Z0-9]", "_")));

            void DispatchOrResume(TopicPartition partition)
            {
                if (!consumer.Assignment.Contains(partition))
                {
                    pending.Remove(partition);
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
                    try
                    {
                        var nextOffset = new TopicPartitionOffset(partition, completed.Value.Batch[^1].Offset + 1);
                        consumer.Commit(new[] { nextOffset });
                        var lag = consumer.Assignment.Select(assigned =>
                            consumer.GetWatermarkOffsets(assigned).High - consumer.Position(assigned)).Sum();
                        consumerOffsets.GetOrAdd(metricKey, Metrics.CreateGauge(metricKey, "offset of kafka topic")).Set(lag);
                    }
                    catch (KafkaException error)
                    {
                        dev.Logger.Instance.Error(error, $"On partition commit {partition} {error.Error.IsFatal}");
                    }
                    DispatchOrResume(partition);
                }
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    FinishCompleted();
                    var consumed = consumer.Consume(TimeSpan.FromMilliseconds(50));
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
            }
            finally
            {
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
