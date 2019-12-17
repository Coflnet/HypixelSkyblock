using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class MessageData
    {
        [IgnoreMember]
        public SkyblockBackEnd Connection;

        [Key("type")]
        public string Type;

        [Key("data")]
        public string Data;

        [Key("mId")]
        public long mId;

        public MessageData(string type, string data)
        {
            Type = type;
            Data = data;
        }
        public MessageData()
        {
        }

        public T GetAs<T>()
        {
            return MessagePackSerializer.Deserialize<T>( MessagePackSerializer.FromJson(Data));
        }

        public void Set<T>(T data)
        {
            Data = MessagePackSerializer.ToJson(data);
        }

        public void SendBack(MessageData data)
        {
            data.mId = mId;
            Connection.SendBack(data);
        }

        public static MessageData Create<T>(string type, T data)
        {
            var d = new MessageData();
            d.Type = type;
            d.Set(data);
            return d;
        }
    }
}
