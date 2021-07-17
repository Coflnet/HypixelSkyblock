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

        public Task<Player> GetPlayer(string uuid)
        {
            using (var context = new HypixelContext())
            {
                return context.Players.Where(p => p.UuId == uuid).FirstOrDefaultAsync();
            }
        }
    }
}