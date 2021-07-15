using System.Threading.Tasks;

namespace hypixel
{
    public class SubscribeCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            SubscribeEngine.Instance.Subscribe(data.GetAs<string>(),data.UserId);
            return data.SendBack(data.Create("subscribeResponse","success"));
        }
    }
    public class UnsubscribeCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            SubscribeEngine.Instance.Unsubscribe(data.GetAs<string>(),data.UserId);
            return data.SendBack(data.Create("unsubscribeResponse","unsubscribed"));
        }
    }
}