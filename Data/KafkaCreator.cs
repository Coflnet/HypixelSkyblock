using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coflnet.Kafka
{
    public class KafkaCreator
    {
        private readonly ILogger<KafkaCreator> _logger;

        public KafkaCreator(ILogger<KafkaCreator> logger)
        {
            _logger = logger;
        }

        public async Task CreateTopicIfNotExist(IConfiguration config, string topic, int partitions = 3)
        {
            config = config.GetSection("Kafka");
            short replicationFactor = config.GetValue<short>("REPLICATION_FACTOR");
            using var adminClient = new AdminClientBuilder(GetClientConfig(config)).Build();
            try
            {
                var meta = adminClient.GetMetadata(topic, TimeSpan.FromSeconds(10));
                if (meta.Topics.Count != 0 && meta.Topics[0].Error.Code == ErrorCode.NoError)
                    return; // topic exists
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"Kafka topic {topic} check");
            }
            try
            {
                await adminClient.CreateTopicsAsync(new TopicSpecification[]
                {
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = partitions,
                        ReplicationFactor = replicationFactor
                    }
                });
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"Kafka topic {topic} create");
            }
        }

        public static AdminClientConfig GetClientConfig(IConfiguration config)
        {
            var baseConfig = new AdminClientConfig
            {
                BootstrapServers = config["BROKERS"],
                SslCaLocation = config["TLS:CA_LOCATION"],
                SslCertificateLocation = config["TLS:CERTIFICATE_LOCATION"],
                SslKeyLocation = config["TLS:KEY_LOCATION"],
                SaslUsername = config["USERNAME"],
                SaslPassword = config["PASSWORD"]
            };
            if(!string.IsNullOrEmpty(baseConfig.SaslUsername))
            {
                if(!string.IsNullOrEmpty(baseConfig.SslKeyLocation))
                    baseConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
                else
                    baseConfig.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                baseConfig.SaslMechanism = SaslMechanism.ScramSha256;
            }
            else
            {
                if(!string.IsNullOrEmpty(baseConfig.SslKeyLocation))
                    baseConfig.SecurityProtocol = SecurityProtocol.Ssl;
                else
                    baseConfig.SecurityProtocol = SecurityProtocol.Plaintext;
            }
            return baseConfig;
        }
        
        private static ProducerConfig GetProducerconfig(IConfiguration config)
        {
            var baseConfig = GetClientConfig(config);
            return new ProducerConfig(baseConfig)
            {
                MessageSendMaxRetries = 10,
                Acks = Acks.All,
                LingerMs = 100,
                BatchNumMessages = 1000,
                CompressionType = CompressionType.Lz4
            };
        }

        public IProducer<TKey, TRes> Produce<TKey,TRes>(IConfiguration config, bool serializeToMsgPack = true)
        {
            config = config.GetSection("Kafka");
            var producerConfig = GetProducerconfig(config);
            if(serializeToMsgPack)
                return new ProducerBuilder<TKey, TRes>(producerConfig).SetValueSerializer(SerializerFactory.GetSerializer<TRes>()).Build();
            return new ProducerBuilder<TKey, TRes>(producerConfig).Build();
        }
    }
}
