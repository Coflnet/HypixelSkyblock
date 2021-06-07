using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebSocketSharp;

namespace hypixel
{
    public class ItemSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var response = context.Items.Include(i => i.Names).ToList();
                data.SendBack(new MessageData("itemSyncResponse", System.Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(response)) ));
            }
        }
    }
}
