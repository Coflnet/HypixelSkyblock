using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public class SearchCommand : Command
    {
        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9_]");
            var search = rgx.Replace(data.Data, "").ToLower();

            var players = PlayerSearch.Instance.Search(search,5);
            data.SendBack(MessageData.Create("searchResponse", players,A_WEEK));

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