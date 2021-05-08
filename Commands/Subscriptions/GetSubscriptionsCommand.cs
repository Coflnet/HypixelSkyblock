using System.Linq;

namespace hypixel
{
    public class GetSubscriptionsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var userId = data.UserId;
                var subs = context.SubscribeItem.Where(s=>s.UserId == userId).ToList();
                data.SendBack(data.Create("subscriptions",subs));
            }
        }
    }
}