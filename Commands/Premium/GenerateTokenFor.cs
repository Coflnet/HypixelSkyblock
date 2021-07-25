using System.Threading.Tasks;

namespace hypixel
{
    public class GenerateTokenFor : Command
    {
        public override Task Execute(MessageData data)
        {
            if(SimplerConfig.Config.Instance["MODE"] != null)
                throw new CoflnetException("not_allowed", "sorry you can't access this, contact me directly");

            return data.SendBack(data.Create("resp", LoginExternal.GenerateToken(data.GetAs<string>())));
        }
    }
}
