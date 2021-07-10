using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
        private const string VALID_MINECRAFT_NAME_CHARS = "abcdefghijklmnopqrstuvwxyz1234567890_";
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

        internal Task<ConcurrentQueue<SearchResultItem>> Search(string search, CancellationToken token)
        {
            if (search.Length > 40)
                return Task.FromResult(new ConcurrentQueue<SearchResultItem>());
            return CreateResponse(search,token);

        }

        static SearchService()
        {
            Instance = new SearchService();
        }

        private async Task Work()
        {
            using (var context = new HypixelContext())
            {
                if (updateCount % 11 == 9)
                    await AddOccurences(context);
                if (updateCount % 10000 == 9999)
                    ShrinkHits(context);
            }
            await SaveHits();
        }

        private async Task AddOccurences(HypixelContext context)
        {
            foreach (var itemId in ItemDetails.Instance.TagLookup.Values)
            {
                var sample = await context.Auctions
                                .Where(a => a.ItemId == itemId)
                                .OrderByDescending(a => a.Id)
                                .Take(20)
                                .Select(a => a.ItemName)
                                .ToListAsync();

                var names = context.AltItemNames.Where(n => n.DBItemId == itemId);
                foreach (var item in names)
                {
                    var occured = sample.Count(s => s == item.Name);
                    if (occured == 0)
                        continue;
                    item.OccuredTimes += occured;
                    context.Update(item);
                }
                await context.SaveChangesAsync();
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }


        public async Task SaveHits()
        {
            using (var context = new HypixelContext())
            {
                //if (updateCount % 12 == 5)
                //    PartialUpdateCache(context);
                ItemDetails.Instance.SaveHits(context);
                PlayerSearch.Instance.SaveHits(context);
                await context.SaveChangesAsync();
            }
            updateCount++;
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
                //PopulateCache();
                while (true)
                {
                    await Task.Delay(10000);
                    try
                    {
                        await Work();
                        await PrefetchCache();
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error("Searchserive got an error " + e.Message + e.StackTrace);
                    }

                }
            }).ConfigureAwait(false);
        }


        private static int prefetchIndex = new Random().Next(1000);
        private async Task PrefetchCache()
        {
            return;
            var charCount = VALID_MINECRAFT_NAME_CHARS.Length;
            var combinations = charCount * charCount + charCount;
            var index = prefetchIndex++ % combinations;
            var requestString = "";
            if (index < charCount)
            {
                requestString = VALID_MINECRAFT_NAME_CHARS[index].ToString();
            }
            else
            {
                index = index - charCount;
                requestString = VALID_MINECRAFT_NAME_CHARS[index / charCount].ToString() + VALID_MINECRAFT_NAME_CHARS[index % charCount];
            }
            await Server.ExecuteCommandWithCache<string, object>("fullSearch", requestString);
        }

        private static async Task<ConcurrentQueue<SearchResultItem>> CreateResponse(string search, CancellationToken token)
        {
            Console.WriteLine("beginsearch");
            var result = new List<SearchResultItem>();

            //var singlePlayer = PlayerSearch.Instance.FindDirect(search);
            var itemTask = ItemDetails.Instance.Search(search, 12);
            var playersTask = PlayerSearch.Instance.Search(search, targetAmount, false);

            var Results = new ConcurrentQueue<SearchResultItem>();
            var searchTasks = new Task[3];
            Console.WriteLine("searching");

            searchTasks[0] = Task.Run(async () =>
            {
                Console.WriteLine("scheduled item wait");
                var items = await itemTask;
                Console.WriteLine("awaited item wait");
                if (items.Count() == 0)// && singlePlayer.Result == null)
                    items = await ItemDetails.Instance.FindClosest(search);

                foreach (var item in items.Select(item => new SearchResultItem(item)))
                {
                    Results.Enqueue(item);
                }
                Console.WriteLine("done item wait");
            },token);

            searchTasks[1] = Task.Run(async () =>
            {
                Console.WriteLine("scheduled player wait");
                foreach (var item in (await playersTask).Select(player => new SearchResultItem(player)))
                    Results.Enqueue(item);
                Console.WriteLine("done player wait");
            },token);

            searchTasks[2] = Task.Run(async () =>
            {
                if (search.Length <= 2)
                    return;
                await Task.Delay(1);
                Console.WriteLine("scheduled last cache wait");
                foreach (var item in await Server.ExecuteCommandWithCache<string, List<SearchResultItem>>("fullSearch", search.Substring(0, search.Length - 2)))
                    Results.Enqueue(item);
                var parts = search.Split(' ');
                if(parts.Count() == 1)
                    return;
                foreach (var item in await Server.ExecuteCommandWithCache<string, List<SearchResultItem>>("fullSearch", parts[1]))
                    Results.Enqueue(item);
            },token);

            foreach (var item in searchTasks)
            {
                item.ConfigureAwait(false);
            }

            var timeout = DateTime.Now + TimeSpan.FromSeconds(2);
            while (DateTime.Now < timeout)
            {
                Console.WriteLine(DateTime.Now);
                if(Results.Count >= 5)
                    return Results;
                await Task.Delay(10);
            }
            Console.WriteLine("=> past timeout");

            Task.WaitAll(searchTasks);
            return Results;
            // return result.OrderBy(r => r.Name?.Length / 2 - r.HitCount - (r.Name?.ToLower() == search.ToLower() ? 10000000 : 0)).Take(targetAmount).ToList();
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
            /// <summary>
            /// Low resolution preview icon
            /// </summary>
            [Key("img")]
            public string Image;
            [IgnoreMember]
            //[Key("hits")]
            public int HitCount;

            public SearchResultItem() { }

            public SearchResultItem(ItemDetails.ItemSearchResult item)
            {
                this.Name = item.Name;
                this.Id = item.Tag;
                this.Type = "item";
                var isPet = IsPet(item);
                if (!item.Tag.StartsWith("POTION") && !isPet && !item.Tag.StartsWith("RUNE"))
                    IconUrl = "https://sky.lea.moe/item/" + item.Tag;
                else
                    this.IconUrl = item.IconUrl;
                if(isPet && !Name.Contains("Pet"))
                    this.Name += " Pet";

                this.HitCount = item.HitCount + ITEM_EXTRA_IMPORTANCE;
                if (ItemReferences.RemoveReforgesAndLevel(Name) != Name)
                    this.HitCount -= NOT_NORMALIZED_PENILTY;
            }

            private static bool IsPet(ItemDetails.ItemSearchResult item)
            {
                return (item.Tag.StartsWith("PET") && !item.Tag.StartsWith("PET_SKIN"));
            }

            public override bool Equals(object obj)
            {
                return obj is SearchResultItem item &&
                       Id == item.Id &&
                       Type == item.Type;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Id, Type);
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
        public class SearchResultComparer : IEqualityComparer<SearchResultItem>
        {
            public bool Equals([AllowNull] SearchResultItem x, [AllowNull] SearchResultItem y)
            {
                return x != null && y != null && x.Equals(y);
            }

            public int GetHashCode([DisallowNull] SearchResultItem obj)
            {
                return obj.GetHashCode();
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