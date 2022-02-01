using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using hypixel;

namespace Coflnet.Kafka
{
    public class KafkaConsumer
    {
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
            try
            {
                var conf = new ConsumerConfig
                {
                    GroupId = groupId,
                    BootstrapServers = host,
                    // Note: The AutoOffsetReset property determines the start offset in the event
                    // there are not yet any committed offsets for the consumer group for the
                    // topic/partitions of interest. By default, offsets are committed
                    // automatically, so in this example, consumption will only start from the
                    // earliest message in the topic 'my-topic' the first time you run the program.
                    AutoOffsetReset = start
                };
                if (deserializer == null)
                    deserializer = SerializerFactory.GetDeserializer<T>();

                using (var c = new ConsumerBuilder<Ignore, T>(conf).SetValueDeserializer(deserializer).Build())
                {
                    c.Subscribe(topic);
                    try
                    {
                        // free the calling task
                        await Task.Yield();
                        while (!cancleToken.IsCancellationRequested)
                        {
                            try
                            {
                                var cr = c.Consume(cancleToken);
                                if (cr == null)
                                    continue;


                                await action(cr.Message.Value);

                                c.Commit(new TopicPartitionOffset[] { cr.TopicPartitionOffset });
                            }
                            catch (ConsumeException e)
                            {
                                dev.Logger.Instance.Error(e, $"Kafka consumer process for {topic}");
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
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"Kafka consumer process for {topic}");
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
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Task ConsumeBatch<T>(string host, string topic, Func<IEnumerable<T>, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            int maxChunkSize = 500,
                                            AutoOffsetReset start = AutoOffsetReset.Earliest)
        {
            return ConsumeBatch<T>(host, new string[] { topic }, action, cancleToken, groupId, maxChunkSize, start);
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
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task ConsumeBatch<T>(string host, string[] topics, Func<IEnumerable<T>, Task> action,
                                            CancellationToken cancleToken,
                                            string groupId = "default",
                                            int maxChunkSize = 500,
                                            AutoOffsetReset start = AutoOffsetReset.Earliest)
        {
            var batch = new Queue<ConsumeResult<Ignore, T>>();
            var conf = new ConsumerConfig
            {
                GroupId = groupId,
                BootstrapServers = Program.KafkaHost,
                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AutoCommitIntervalMs = 0
            };
            // in case this method is awaited on in a backgroundWorker
            await Task.Yield();

            using (var c = new ConsumerBuilder<Ignore, T>(conf).SetValueDeserializer(SerializerFactory.GetDeserializer<T>()).Build())
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
                            while (batch.Count < maxChunkSize)
                            {
                                cr = c.Consume(TimeSpan.Zero);
                                if (cr == null)
                                {
                                    break;
                                }
                                batch.Enqueue(cr);
                            }
                            await action(batch.Select(a => a.Message.Value));
                            // tell kafka that we stored the batch
                            c.Commit(batch.Select(b => b.TopicPartitionOffset));
                            batch.Clear();
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
