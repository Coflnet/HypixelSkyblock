using System.Threading.Tasks;

namespace hypixel
{
    public class SetMyReferalCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            ReferalService.Instance.WasReferedBy(data.User, data.GetAs<string>());
            return data.Ok();
        }
    }
}
