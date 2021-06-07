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
                var batchAmount = 5000;
                var offset = data.GetAs<int>();
                if(offset != 0)
                    offset -= 120; // two update batch wide overlap
                while (!done)
                {
                    var response = context.Players.Skip(offset + batchAmount * index++).Take(batchAmount).ToList();
                    if (response.Count == 0)
                        return;
                    data.SendBack(new MessageData("playerSyncResponse", System.Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(response))));
                }
            }
        }
    }
}
