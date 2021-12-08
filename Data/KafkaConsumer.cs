using System;
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
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task Consume<T>(string host, string topic, Func<T, Task> action, 
                                            CancellationToken cancleToken, 
                                            string groupId = "default", 
                                            AutoOffsetReset start = AutoOffsetReset.Earliest)
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

                using (var c = new ConsumerBuilder<Ignore, T>(conf).SetValueDeserializer(SerializerFactory.GetDeserializer<T>()).Build())
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
    }
}
