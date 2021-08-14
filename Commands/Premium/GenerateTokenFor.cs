using System.Threading.Tasks;

namespace hypixel
{
    public class GenerateTokenFor : Command
    {
        private static string MODE = SimplerConfig.Config.Instance["MODE"];
        public override Task Execute(MessageData data)
        {
            if(MODE != null)
                throw new CoflnetException("not_allowed", "sorry you can't access this, contact me directly");

            return data.SendBack(data.Create("resp", LoginExternalCommand.GenerateToken(data.GetAs<string>())));
        }
    }
}
