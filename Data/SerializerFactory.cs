using System;
using Confluent.Kafka;

namespace Coflnet.Sky.Core
{
    public class SerializerFactory
    {
        public static IDeserializer<T> GetDeserializer<T>()
        {
            return new MsgPackDeserializer<T>();
        }

        public static ISerializer<T> GetSerializer<T>()
        {
            if (typeof(T) == typeof(Ignore))
                return new IgnoreSerializer<T>();
            return new MsgPackSerializer<T>();
        }

        public class IgnoreSerializer<T> : ISerializer<T>
        {
            public byte[] Serialize(T data, SerializationContext context)
            {
                return Array.Empty<byte>();
            }
        }
    }

    public class MsgPackDeserializer<T> : IDeserializer<T>
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            return MessagePack.MessagePackSerializer.Deserialize<T>(data.ToArray());
        }
    }

    public class MsgPackSerializer<T> : ISerializer<T>
    {
        public byte[] Serialize(T data, SerializationContext context)
        {
            return MessagePack.MessagePackSerializer.Serialize(data);
        }
    }
}