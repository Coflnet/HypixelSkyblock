using System.Threading.Tasks;

namespace hypixel
{
    public class PingCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return Task.CompletedTask;
        }
    }
}
