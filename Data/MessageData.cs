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

        [Key("maxAge")]
        public int MaxAge;

        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public string CustomCacheKey;
        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public GoogleUser User => UserService.Instance.GetUserById(Connection.UserId);

        public MessageData(string type, string data, int maxAge = 0)
        {
            Type = type;
            Data = data;
            MaxAge = maxAge;
        }
        public MessageData()
        {
        }

        public T GetAs<T>()
        {
            return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.FromJson(Data));
        }

        private int responseCounter = 0;

        public void SendBack(MessageData data, bool cache = true)
        {
            data.mId = mId;
            if (cache)
                CacheService.Instance.Save(this, data, responseCounter++);
            Connection.SendBack(data);
        }

        public static MessageData Create<T>(string type, T data, int maxAge = 0)
        {
            var d = new MessageData(type, MessagePackSerializer.ToJson(data), maxAge);
            return d;
        }

        public static MessageData Copy(MessageData original)
        {
            var d = new MessageData(original.Type, original.Data, original.MaxAge);
            return d;
        }
    }
}
