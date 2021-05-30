using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class BackWardsNBTIndexer
    {
        public int minId;

        public BackWardsNBTIndexer(int minId)
        {
            this.minId = minId;
        }
        
        public async Task DoBatch()
        {
            await Task.Delay(8765);
            var batchSize = 2000;
            using(var context = new HypixelContext())
            {
                var select = context.Auctions
                        .Where(a=>a.Id < minId)
                        .OrderByDescending(a=>a.Id)
                        .Include(a=>a.NBTLookup)
                        .Include(a=>a.NbtData)
                        .Take(batchSize);
                foreach (var auction in select)
                {
                    if(auction.NBTLookup != null && auction.NBTLookup.Count > 0)
                        continue;
                    auction.NBTLookup = NBT.CreateLookup(auction.NbtData);
                    context.Update(auction);
                }
                int updated = await context.SaveChangesAsync();
                Console.WriteLine($"updated nbt lookup for {updated} auctions, highest: {minId}");
                minId-=batchSize;
            }
        }
    }
}