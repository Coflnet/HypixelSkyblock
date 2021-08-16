using System;
using Confluent.Kafka;

namespace hypixel
{
    public class SerializerFactory
    {
        public static IDeserializer<T> GetDeserializer<T>()
        {
            return new MsgPackDeserializer<T>();
        }

        public static ISerializer<T> GetSerializer<T>()
        {
            return new MsgPackSerializer<T>();
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