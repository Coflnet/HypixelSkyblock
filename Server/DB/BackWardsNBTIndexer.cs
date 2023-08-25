using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Core
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
            await Task.Delay(TimeSpan.FromMinutes(5));
            var batchSize = 2000;
            using (var context = new HypixelContext())
            {
                var select = context.Auctions
                        .Where(a => a.Id < minId)
                        .OrderByDescending(a => a.Id)
                        .Include(a => a.NBTLookup)
                        .Include(a => a.NbtData)
                        .Take(batchSize);
                foreach (var auction in select)
                {
                    if (auction.NBTLookup != null && auction.NBTLookup.Count() > 0)
                        continue;
                    try
                    {
                        auction.NBTLookup = NBT.CreateLookup(auction).ToArray();
                        context.Update(auction);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"could not generate nbtlookup for {auction.Uuid} {e.Message} \n{e.StackTrace} \n {e.InnerException?.Message} {e.InnerException.StackTrace}" );
                    }
                }
                int updated = await context.SaveChangesAsync();
                Console.WriteLine($"updated nbt lookup for {updated} auctions, highest: {minId}");
                minId -= batchSize;
            }
        }
    }
}