using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;

namespace hypixel
{
    public class FullSearchCommand : Command
    {
        private const string Type = "searchResponse";

        public override void Execute(MessageData data)
        {
            var watch = Stopwatch.StartNew();
            Regex rgx = new Regex("[^a-zA-Z0-9_\\. ]");
            var search = rgx.Replace(data.Data, "").ToLower();
            var cancelationSource = new CancellationTokenSource();
            var results = SearchService.Instance.Search(search, cancelationSource.Token);

            var result = new ConcurrentBag<SearchService.SearchResultItem>();
            var pullTask = Task.Run(async () =>
            {
                Console.WriteLine($"Started task " + watch.Elapsed);
                while (results.Result.TryDequeue(out SearchService.SearchResultItem r))
                {
                    Console.WriteLine($"got partial search result {r.Name} {watch.Elapsed}");
                    result.Add(r);
                    if (result.Count >= 15)
                        return; // return early

                    Task.Run(() => LoadPreview(watch, r),cancelationSource.Token).ConfigureAwait(false);
                }
            }, cancelationSource.Token);

            Console.WriteLine($"Waiting half a second " + watch.Elapsed);
            pullTask.Wait(320);
            while (results.Result.TryDequeue(out SearchService.SearchResultItem r))
                result.Add(r);
            Console.WriteLine($"Waited half a second " + watch.Elapsed);

            var maxAge = 60;//A_DAY / 2;

            if (result.Count == 0)
                maxAge = A_MINUTE;
            cancelationSource.Cancel();
            Console.WriteLine($"Started sorting " + watch.Elapsed);
            var orderedResult = result.OrderBy(r => r.Name?.Length / 2 - r.HitCount
                            - (r.Name?.ToLower() == search.ToLower() ? 10000000 : 0)
                            - (!String.IsNullOrEmpty(r.Name) && r.Name.Length > search.Length && r.Name.ToLower().Truncate(search.Length) == search.ToLower() ? 50 : 0)
                            + Fastenshtein.Levenshtein.Distance(r.Name, search))
            .Distinct(new SearchService.SearchResultComparer()).Take(5).ToList();
            Console.WriteLine($"making response " + watch.Elapsed);

            data.SendBack(data.Create(Type, orderedResult, maxAge));
            Task.Run(() =>
            {
                if (!(data is Server.ProxyMessageData<string, object>))
                    TrackingService.Instance.TrackSearch(data, search, orderedResult.Count, watch.Elapsed);
            }).ConfigureAwait(false);
        }

        private async Task LoadPreview(Stopwatch watch, SearchService.SearchResultItem r)
        {
            try
            {
                PreviewService.Preview preview = null;
                if (r.Type == "player")
                    preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("pPrev", r.Id);
                else if (r.Type == "item")
                    preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("iPrev", r.Id);

                if (preview == null)
                    return;

                Console.WriteLine($"Loaded image {r.Name} " + watch.Elapsed);
                r.Image = preview.Image;
                r.IconUrl = preview.ImageUrl;
            } catch(Exception e)
            {
                dev.Logger.Instance.Error(e, "Failed to load preview for " + r.Id);
            }


        }
    }
}