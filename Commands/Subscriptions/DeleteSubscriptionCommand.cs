using System.Linq;
using MessagePack;

namespace hypixel
{
    public class DeleteSubscriptionCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var args = data.GetAs<Arguments>();
                var userId = data.UserId;
                
                var affected = SubscribeEngine.Instance.Unsubscribe(userId, args.Topic,args.Type).Result;

                data.SendBack(data.Create("unsubscribed", affected));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("userId")]
            public string UserId;
            [Key("topic")]
            public string Topic;
            [Key("type")]
            public SubscribeItem.SubType Type;
        }
    }
}