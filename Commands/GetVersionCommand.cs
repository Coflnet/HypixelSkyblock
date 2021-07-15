using System.Threading.Tasks;

namespace hypixel
{
    public class GetVersionCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(data.Create("version",Program.Version,A_DAY));
        }
    }
}
