using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Coflnet;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;

namespace Coflnet.Sky.Core
{
    public class PlayerSearch
    {
        public static PlayerSearch Instance;

        ConcurrentDictionary<string, int> playerHits = new ConcurrentDictionary<string, int>();

        static PlayerSearch()
        {
            Instance = new PlayerSearch();
        }

        public async Task<string> GetName(string uuid)
        {
            using (var context = new HypixelContext())
            {
                return await context.Players
                    .Where(player => player.UuId == uuid)
                    .Select(player => player.Name)
                    .FirstOrDefaultAsync();
            }
        }

        public string GetIdForName(string name)
        {
            return GetIdForNameAsync(name).Result;
        }

        public async Task<string> GetIdForNameAsync(string name)
        {
            using (var context = new HypixelContext())
            {
                return await context.Players
                    .Where(player => player.Name == name)
                    .Select(player => player.UuId)
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<Player> GetPlayerAsync(string uuid)
        {
            using (var context = new HypixelContext())
            {
                return await context.Players
                    .Where(player => player.UuId == uuid)
                    .FirstOrDefaultAsync();
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
            var searchPattern = search.Replace("_", "\\_");
            System.Console.WriteLine("searching player " + search);

            using (var context = new HypixelContext())
            {
                var baseSelect = context.Players
                    .Where(e => EF.Functions.Like(e.Name, $"{searchPattern}%"));
                if (search.Length > 15)
                {
                    baseSelect = context.Players.Where(e => EF.Functions.Like(e.Name, $"{searchPattern}%") || e.UuId == search);
                    if (long.TryParse(search, out var parsed) && search.Length < 30)
                    {
                        var convertedUuid = AuctionService.Instance.GetUuid(parsed);
                        baseSelect = context.Players.Where(e => EF.Functions.Like(e.UuId, $"{convertedUuid}%"));
                    }
                }
                result = await baseSelect
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

        private async Task LoadPlayerName(string search, bool forceResolution, List<PlayerResult> result)
        {
            var profile = await GetMcProfile(search);
            if (profile == null && forceResolution)
                throw new CoflnetException("player_not_found", $"we don't know of a player with the name {search}");

            result.Add(new PlayerResult(profile.Name, profile.Id));
        }

        public async Task<MinecraftProfile> GetMcProfile(string name)
        {
            var client = new RestClient("https://api.mojang.com/");
            var request = new RestRequest($"/users/profiles/minecraft/{name}", Method.Get);
            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                return JsonConvert.DeserializeObject<MinecraftProfile>(response.Content);

            client = new RestClient("https://playerdb.co/");
            request = new RestRequest($"/api/player/minecraft/{name}", Method.Get);
            response = await client.ExecuteAsync(request);
            Console.WriteLine($"PlayerDB response: {response.StatusCode} - {response.Content}");
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                return null;
            var responseData = JsonConvert.DeserializeObject<PlayerDbResponse>(response.Content).Data;
            Console.WriteLine($"PlayerDB response: {responseData.Player.RawId} - {responseData.Player.Username}");
            return new MinecraftProfile
            {
                Id = responseData.Player.RawId,
                Name = responseData.Player.Username
            };
        }

        public class PlayerDbResponse
        {
            [JsonProperty("data")]
            public PlayerDbData Data { get; set; }
        }
        public class PlayerDbData
        {
            [JsonProperty("player")]
            public PlayerDbPlayer Player { get; set; }
        }
        public class PlayerDbPlayer
        {
            [JsonProperty("raw_id")]
            public string RawId { get; set; }

            [JsonProperty("username")]
            public string Username { get; set; }
        }

        public class MinecraftProfile
        {
            [JsonProperty("id")]
            public string Id { get; init; }

            [JsonProperty("name")]
            public string Name { get; init; }
        }
    }
}