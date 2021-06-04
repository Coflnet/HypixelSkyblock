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
                data.SendBack(data.Create("itemSyncResponse", CacheService.Zip(MessagePack.MessagePackSerializer.ToJson(response))));
            }
        }
    }
}
