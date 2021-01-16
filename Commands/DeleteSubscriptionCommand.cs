using System.Linq;
using MessagePack;

namespace hypixel
{
    public class DeleteSubscriptionCommand : Command
    {
        public override async void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var args = data.GetAs<Arguments>();
                var userId = data.Connection.UserId;
                var subs = context.SubscribeItem.Where(s => s.UserId == userId && s.TopicId == args.Topic && s.Type == args.Type).FirstOrDefault();
                if (subs != null)
                    context.SubscribeItem.Remove(subs);
                var affected = await context.SaveChangesAsync();

                data.SendBack(MessageData.Create("unsubscribed", affected));
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