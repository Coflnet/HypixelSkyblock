using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using static hypixel.FullSearchCommand;

namespace hypixel
{
    public class SearchService
    {
        const int targetAmount = 5;



        ConcurrentDictionary<string, CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();
        ConcurrentQueue<PopularSite> popularSite = new ConcurrentQueue<PopularSite>();

        private int updateCount = 0;
        public static SearchService Instance { get; private set; }

        internal void AddPopularSite(string type, string id)
        {
            string title = "";
            if (type == "player")
                title = PlayerSearch.Instance.GetNameWithCache(id) + " auctions hypixel skyblock";
            else if (type == "item")
                title = ItemDetails.TagToName(id) + " price hypixel skyblock";
            var entry = new PopularSite(title, $"{type}/{id}");
            if (!popularSite.Contains(entry))
                popularSite.Enqueue(entry);
            if (popularSite.Count > 100)
                popularSite.TryDequeue(out PopularSite result);
        }

        public IEnumerable<PopularSite> GetPopularSites()
        {
            return popularSite;
        }

        internal async Task<List<SearchResultItem>> Search(string search)
        {
            if (search.Length > 40)
                return null;
            if (!cache.TryGetValue(search, out CacheItem result))
            {
                result = await CreateAndCache(search);
            }
            result.hitCount++;
            return result.response;
        }

        private async Task<CacheItem> CreateAndCache(string search)
        {
            CacheItem result;
            var response = await CreateResponse(search);
            result = new CacheItem(response);
            cache.AddOrUpdate(search, result, (key, old) => result);
            return result;
        }

        static SearchService()
        {
            Instance = new SearchService();
        }

        private async Task Work()
        {
            using (var context = new HypixelContext())
            {

                if (updateCount % 10000 == 9999)
                    ShrinkHits(context);
                if (updateCount % 12 == 5)
                    PartialUpdateCache(context);
                ItemDetails.Instance.SaveHits(context);
                PlayerSearch.Instance.SaveHits(context);
                await context.SaveChangesAsync();
            }
            updateCount++;
        }

        private void PartialUpdateCache(HypixelContext context, int maxUpdateCount = 10)
        {
            var maxAge = DateTime.Now - TimeSpan.FromHours(1);
            var toDelete = cache.Where(item => item.Value.hitCount < 2 &&
                    item.Key.Length > 2 &&
                    item.Value.created < maxAge)
                .Select(item => item.Key).ToList();

            foreach (var item in toDelete)
            {
                cache.TryRemove(item, out CacheItem element);
            }
            var update = cache.Where(el => el.Value.hitCount > 1)
                .OrderBy(el => el.Value.created)
                .OrderByDescending(el => el.Value.hitCount)
                .Select(el => el.Key)
                .Take(maxUpdateCount)
                .ToList();
            foreach (var item in update)
            {
                CreateAndCache(item);
            }
            if (update.Any())
                Console.WriteLine($"cached search for {update.First()} ");
        }

        private void ShrinkHits(HypixelContext context)
        {
            Console.WriteLine("shrinking hits !!");
            ShrinkHitsType(context, context.Players);
            ShrinkHitsType(context, context.Items);
        }

        private static void ShrinkHitsType(HypixelContext context, IEnumerable<IHitCount> source)
        {
            // heavy searched results are reduced in order to allow other results to overtake them
            var res = source.Where(p => p.HitCount > 4);
            foreach (var item in res)
            {
                item.HitCount = item.HitCount * 9 / 10; // - 1; players that were searched once will be prefered forever
                context.Update(item);
            }
        }

        internal void RunForEver()
        {
            Task.Run(async () =>
            {
                PopulateCache();
                while (true)
                {
                    await Task.Delay(10000);
                    try
                    {
                        await Work();
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error("Searchserive got an error " + e.Message + e.StackTrace);
                    }

                }
            });
        }

        private async void PopulateCache()
        {
            var letters = "abcdefghijklmnopqrstuvwxyz1234567890_";

            foreach (var letter in letters)
            {
                try
                {

                    await CreateAndCache(letter.ToString());
                    await Task.Delay(100);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Search service cache {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace}");
                }
            }
            //await CreateAndCache("");
            Console.WriteLine("populated Cache");
        }

        private static async Task<List<SearchResultItem>> CreateResponse(string search)
        {
            var result = new List<SearchResultItem>();

            var items = await ItemDetails.Instance.Search(search, 20);
            var players = await PlayerSearch.Instance.Search(search, targetAmount, false);

            if (items.Count() == 0 && players.Count() == 0)
                items = await ItemDetails.Instance.FindClosest(search);

            result.AddRange(items.Select(item => new SearchResultItem(item)));
            result.AddRange(players.Select(player => new SearchResultItem(player)));

            return result.OrderBy(r => r.Name?.Length / 2 - r.HitCount - (r.Name?.ToLower() == search.ToLower() ? 10000000 : 0))
                .Take(targetAmount).ToList();
        }

        class CacheItem
        {
            public List<SearchResultItem> response;
            public int hitCount;
            public DateTime created;

            public CacheItem(List<SearchResultItem> response)
            {
                this.response = response;
                this.created = DateTime.Now;
                this.hitCount = 0;
            }
        }

        [MessagePackObject]
        public class SearchResultItem
        {
            private const int ITEM_EXTRA_IMPORTANCE = 10;
            private const int NOT_NORMALIZED_PENILTY = ITEM_EXTRA_IMPORTANCE * 3 / 2;
            [Key("name")]
            public string Name;
            [Key("id")]
            public string Id;
            [Key("type")]
            public string Type;
            [Key("iconUrl")]
            public string IconUrl;
            //[IgnoreMember]
            [Key("hits")]
            public int HitCount;

            public SearchResultItem() { }

            public SearchResultItem(ItemDetails.ItemSearchResult item)
            {
                this.Name = item.Name;
                this.Id = item.Tag;
                this.Type = "item";
                if (!item.Tag.StartsWith("POTION") && IsPet(item) && !item.Tag.StartsWith("RUNE"))
                    IconUrl = "https://sky.lea.moe/item/" + item.Tag;
                else
                    this.IconUrl = item.IconUrl;
                this.HitCount = item.HitCount + ITEM_EXTRA_IMPORTANCE;
                if (ItemReferences.RemoveReforgesAndLevel(Name) != Name)
                    this.HitCount -= NOT_NORMALIZED_PENILTY;
            }

            private static bool IsPet(ItemDetails.ItemSearchResult item)
            {
                return (!item.Tag.StartsWith("PET") || item.Tag.StartsWith("PET_SKIN"));
            }

            public SearchResultItem(PlayerResult player)
            {
                this.Name = player.Name;
                this.Id = player.UUid;
                this.IconUrl = PlayerHeadUrl(player.UUid);
                this.Type = "player";
                this.HitCount = player.HitCount;
            }


        }

        public static string PlayerHeadUrl(string playerUuid)
        {
            return "https://crafatar.com/avatars/" + playerUuid;
        }
    }

    public interface IHitCount
    {
        int HitCount { get; set; }
    }
}