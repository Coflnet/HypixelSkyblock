using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Core
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
    }
}