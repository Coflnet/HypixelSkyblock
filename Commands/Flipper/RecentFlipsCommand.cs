using System.Linq;

namespace hypixel
{
    public class RecentFlipsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var flipps = Flipper.FlipperEngine.Instance.Flipps.Take(50);
            data.SendBack(data.Create("flips",flipps,A_MINUTE));
        }
    }
}