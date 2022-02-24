using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class PlayerService
    {
        public static PlayerService Instance;
        static PlayerService()
        {
            Instance = new PlayerService();
        }

        public async Task<Player> GetPlayer(string uuidOrName)
        {
            using (var context = new HypixelContext())
            {
                return await context.Players.Where(p => p.UuId == uuidOrName || p.Name == uuidOrName).FirstOrDefaultAsync();
            }
        }
        public async Task<Player> UpdatePlayerName(string uuid)
        {
            using (var context = new HypixelContext())
            {
                dev.Logger.Instance.Info($"Name update request for {uuid}");
                var player = await context.Players.Where(p => p.UuId == uuid).FirstOrDefaultAsync();
                if (player == null)
                {
                    player = new Player(uuid);
                    context.Add(player);
                }
                else
                    context.Update(player);
                player.ChangedFlag = true;
                await context.SaveChangesAsync();
                return player;
            }
        }
    }
}