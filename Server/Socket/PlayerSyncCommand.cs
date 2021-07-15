using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using WebSocketSharp;

namespace hypixel
{
    public class PlayerSyncCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var batchAmount = 5000;
                var offset = data.GetAs<int>();
                if(offset != 0)
                    offset -= 120; // two update batch wide overlap
                
                var response = new PlayerSyncData(context.Players.Skip(offset).Take(batchAmount).ToList(),offset+batchAmount);
                
                return data.SendBack(new MessageData("playerSyncResponse", System.Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(response))));
            
            }
        }

        [MessagePackObject]
        public class PlayerSyncData
        {
            [Key(0)]
            public List<Player> Players;
            [Key(1)]
            public int Offset;

            public PlayerSyncData()
            {
            }

            public PlayerSyncData(List<Player> players, int offset)
            {
                Players = players;
                Offset = offset;
            }

            
        }
    }
}
