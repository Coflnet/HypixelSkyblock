using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public class PlayerSearch
    {
        public static PlayerSearch Instance;

        ConcurrentDictionary<string, int> playerHits = new ConcurrentDictionary<string, int>();

        static PlayerSearch()
        {
            Instance = new PlayerSearch();
        }

        public string GetName(string uuid)
        {
            using (var context = new HypixelContext())
            {
                return context.Players
                    .Where(player => player.UuId == uuid)
                    .Select(player => player.Name)
                    .FirstOrDefault();
            }
        }
        public string GetIdForName(string name)
        {
            using (var context = new HypixelContext())
            {
                return context.Players
                    .Where(player => player.Name == name)
                    .Select(player => player.UuId)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Uses the <see cref="CacheService"/> to cache db queries
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public string GetNameWithCache(string uuid)
        {
            var task = GetNameWithCacheAsync(uuid);
            task.Wait();
            return task.Result;
        }

        public Task<string> GetNameWithCacheAsync(string uuid)
        {
            return CoreServer.ExecuteCommandWithCache<string, string>("playerName", uuid);
        }


        /// <summary>
        /// Registers that a specific player was looked up and modifies the search order acordingly
        /// </summary>
        /// <param name="name"></param>
        public void AddHitFor(string uuid)
        {
            playerHits.AddOrUpdate(uuid, 1, (key, value) => value + 1);
        }

        public void SaveHits(HypixelContext context)
        {
            var hits = playerHits;
            playerHits = new ConcurrentDictionary<string, int>();
            foreach (var hit in hits)
            {
                var player = context.Players.Where(player => player.UuId == hit.Key).FirstOrDefault();
                if (player == null)
                    continue;
                player.HitCount += hit.Value;
                context.Update(player);
            }
        }

        public void SaveNameForPlayer(string name, string uuid)
        {
            //Console.WriteLine($"Saving {name} ({uuid})");
            var index = name.Substring(0, 3).ToLower();
            string path = "players/" + index;
            lock (path)
            {
                HashSet<PlayerResult> list = null;
                if (FileController.Exists(path))
                    list = FileController.LoadAs<HashSet<PlayerResult>>(path);
                if (list == null)
                    list = new HashSet<PlayerResult>();
                list.Add(new PlayerResult(name, uuid));
                FileController.SaveAs(path, list);
            }
        }

        public async Task<PlayerResult> FindDirect(string search)
        {
            using (var context = new HypixelContext())
            {
                return await context.Players.Where(p => p.Name == search)
                    .Select(p => new PlayerResult(p.Name, p.UuId, p.HitCount + 10000000))
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<IEnumerable<PlayerResult>> Search(string search, int count, bool forceResolution = true)
        {
            if (count <= 0 || search.Contains(' '))
                return new PlayerResult[0];

            List<PlayerResult> result;
            search = search.Replace("_", "\\_");

            using (var context = new HypixelContext())
            {
                result = await context.Players
                    .Where(e => EF.Functions.Like(e.Name, $"{search}%") || e.UuId == search)
                    .OrderBy(p => p.Name.Length - p.HitCount - (p.Name == search || p.UuId == search ? 10000000 : 0))
                    .Select(p => new PlayerResult(p.Name, p.UuId, p.HitCount))
                    .Take(count)
                    .ToListAsync();

                if (result.Count() == 0)
                {
                    await LoadPlayerName(search, forceResolution, result);
                }

            }
            return result;
        }

        private static async Task LoadPlayerName(string search, bool forceResolution, List<PlayerResult> result)
        {
            var client = new RestClient("https://mc-heads.net/");
            var request = new RestRequest($"/minecraft/profile/{search}", Method.GET);
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                if (forceResolution)
                    throw new CoflnetException("player_not_found", $"we don't know of a player with the name {search}");
                else
                { // nothing to do 
                }
            else
            {
                var value = JsonConvert.DeserializeObject<MinecraftProfile>(response.Content);
                NameUpdater.UpdateUUid(value.Id, value.Name);
                result.Add(new PlayerResult(value.Name, value.Id));
            }
        }

        public class MinecraftProfile
        {
            [JsonProperty("id")]
            public string Id { get; private set; }

            [JsonProperty("name")]
            public string Name { get; private set; }
        }
    }
}