using System.Threading.Tasks;

namespace hypixel
{
    public class GetRefInfoCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var refInfo = ReferalService.Instance.GetReferalInfo(data.User);
            return data.SendBack(data.Create("refInfo", refInfo));
        }
    }
}
