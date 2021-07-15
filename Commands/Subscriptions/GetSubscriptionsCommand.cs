using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class GetSubscriptionsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var userId = data.UserId;
                var subs = context.SubscribeItem.Where(s=>s.UserId == userId).ToList();
                return data.SendBack(data.Create("subscriptions",subs));
            }
        }
    }
}