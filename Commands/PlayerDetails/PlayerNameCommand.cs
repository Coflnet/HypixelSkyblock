using System.Threading.Tasks;

namespace hypixel
{
    public class PlayerNameCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var respone = CreateResponse(data, data.GetAs<string>());
            return data.SendBack(respone);
        }

        public static MessageData CreateResponse(MessageData data, string uuid)
        {
            var name = PlayerSearch.Instance.GetName(uuid);
            // player names don't change often, but are easy to compute
            return data.Create("nameResponse",name,A_HOUR);
        }
    }
}