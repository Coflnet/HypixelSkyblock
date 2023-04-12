using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Prometheus;

namespace Coflnet.Kafka
{
    public class KafkaConsumer
    {

        static Counter processFail = Metrics.CreateCounter("consume_process_failed", "How often processing of consumed messages failed");
        /// <summary>
        /// Generic consumer
        /// </summary>
        /// <param name="host"></param>
        /// <param name="topic"></param>
        /// <param name="action"></param>
        /// <param name="cancleToken"></param>
        /// <param name="groupId"></param>
        /// <param name="start">What event to start at</param>
        /// <param name="deserializer">The deserializer used for new messages</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task Consume<T>(string host, string topic, Func<T, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            while (!cancleToken.IsCancellationRequested)
                try
                {
                    await ConsumeBatch(host, topic, async batch =>
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
        /// <param name="host"></param>
        /// <param name="topic"></param>
        /// <param name="action"></param>
        /// <param name="cancleToken"></param>
        /// <param name="groupId"></param>
        /// <param name="maxChunkSize"></param>
        /// <param name="start"></param>
        /// <param name="deserializer"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Task ConsumeBatch<T>(string host, string topic, Func<IEnumerable<T>, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            int maxChunkSize = 500,
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            return ConsumeBatch<T>(host, new string[] { topic }, action, cancleToken, groupId, maxChunkSize, start, deserializer);
        }

        /// <summary>
        /// Consume a batch of messages for multiple topics 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="topics"></param>
        /// <param name="action"></param>
        /// <param name="cancleToken"></param>
        /// <param name="groupId"></param>
        /// <param name="maxChunkSize"></param>
        /// <param name="start"></param>
        /// <param name="deserializer"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task ConsumeBatch<T>(string host, string[] topics, Func<IEnumerable<T>, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            int maxChunkSize = 500,
                                            AutoOffsetReset start = AutoOffsetReset.Earliest,
                                            IDeserializer<T> deserializer = null)
        {
            await ConsumeBatch(new ConsumerConfig
            {
                GroupId = groupId,

                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = start,
                BootstrapServers = host,
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
            var currentChunkSize = 1;

            using (var c = new ConsumerBuilder<Ignore, T>(conf).SetValueDeserializer(deserializer).Build())
            {
                c.Subscribe(topics);
                try
                {
                    while (!cancleToken.IsCancellationRequested)
                    {
                        try
                        {
                            var cr = c.Consume(cancleToken);
                            batch.Enqueue(cr);
                            while (batch.Count < currentChunkSize)
                            {
                                cr = c.Consume(TimeSpan.Zero);
                                if (cr == null)
                                {
                                    break;
                                }
                                batch.Enqueue(cr);
                            }
                            await action(batch.Select(a => a.Message.Value)).ConfigureAwait(false);
                            // tell kafka that we stored the batch
                            if (!config.EnableAutoCommit ?? true)
                                try
                                {
                                    c.Commit(batch.Select(b => b.TopicPartitionOffset));
                                }
                                catch (KafkaException e)
                                {
                                    dev.Logger.Instance.Error(e, $"On commit {string.Join(',', topics)} {e.Error.IsFatal}");
                                }
                            batch.Clear();
                            if(currentChunkSize < maxChunkSize)
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
                    // Ensure the consumer leaves the group cleanly and final offsets are committed.
                    c.Close();
                }
            }
        }
    }
}
