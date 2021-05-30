using System.Linq;

namespace hypixel
{
    public class RecentFlipsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var flipps = Flipper.FlipperEngine.Instance.Flipps.Take(50);
            try {
                if (data.UserId != 0)
                    flipps = Flipper.FlipperEngine.Instance.Flipps.Skip(50).Take(50);
                if (data.User.HasPremium)
                    flipps = Flipper.FlipperEngine.Instance.Flipps.Reverse().Take(50);
            } catch(CoflnetException)
            {
                // no premium, continue
            }
            data.SendBack(data.Create("flips",flipps,A_MINUTE));
        }
    }
}