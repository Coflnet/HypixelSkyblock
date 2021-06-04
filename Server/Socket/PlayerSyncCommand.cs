using System.Linq;
using WebSocketSharp;

namespace hypixel
{
    public class PlayerSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var done = false;
                var index = 0;
                var batchAmount = 10000;
                while (!done)
                {
                    var response = context.Players.Skip(batchAmount * index++).Take(batchAmount).ToList();
                    if (response.Count == 0)
                        return;
                    data.SendBack(data.Create("playerSyncResponse", response));
                }
            }
        }
    }
}
