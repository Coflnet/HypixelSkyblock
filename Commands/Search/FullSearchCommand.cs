using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;

namespace hypixel
{
    public class FullSearchCommand : Command
    {
        private const string Type = "searchResponse";

        public override Task Execute(MessageData data)
        {
            var watch = Stopwatch.StartNew();
            var search = ItemSearchCommand.RemoveInvalidChars(data.Data);
            var cancelationSource = new CancellationTokenSource();
            var results = SearchService.Instance.Search(search, cancelationSource.Token);

            var result = new ConcurrentBag<SearchService.SearchResultItem>();
            var pullTask = Task.Run(async () =>
            {
                Console.WriteLine($"Started task " + watch.Elapsed);
                while (results.Result.TryDequeue(out SearchService.SearchResultItem r))
                {
                    result.Add(r);
                    if (result.Count > 15)
                        return; // return early

                    Task.Run(() => LoadPreview(watch, r), cancelationSource.Token).ConfigureAwait(false);
                }
            }, cancelationSource.Token);

            Console.WriteLine($"Waiting half a second " + watch.Elapsed);
            pullTask.Wait(320);
            DequeueResult(results, result);
            if (result.Count == 0)
            {
                pullTask.Wait(600);
                DequeueResult(results, result);
            }
            Console.WriteLine($"Waited half a second " + watch.Elapsed);

            var maxAge = A_DAY / 2;

            cancelationSource.Cancel();
            DequeueResult(results, result);
            Console.WriteLine($"Started sorting {search} " + watch.Elapsed);
            var orderedResult = result.Where(r => r.Name != null)
                            .Select(r =>
                            {
                                var lower = r.Name.ToLower();
                                return new
                                {
                                    rating = String.IsNullOrEmpty(r.Name) ? 0 :
                                lower.Length / 2
                                - r.HitCount * 2
                                - (lower == search ? 10000000 : 0) // is exact match
                                - (lower.Length > search.Length && lower.Truncate(search.Length) == search ? 100 : 0) // matches search
                                - (Fastenshtein.Levenshtein.Distance(lower, search) <= 1 ? 40 : 0) // just one mutation off maybe a typo
                                + Fastenshtein.Levenshtein.Distance(lower.PadRight(search.Length), search) / 2 // distance to search
                                + Fastenshtein.Levenshtein.Distance(lower.Truncate(search.Length), search),
                                    r
                                };
                            }
                            )
                            .OrderBy(r => r.rating)
                        .Where(r => { Console.WriteLine($"Ranked {r.r.Name} {r.rating} {Fastenshtein.Levenshtein.Distance(r.r.Name.PadRight(search.Length), search) / 10} {Fastenshtein.Levenshtein.Distance(r.r.Name.Truncate(search.Length), search)}"); return true; })
                        .Where(r => r.rating < 10)
                        .ToList()
                        .Select(r => r.r)
                        .Distinct(new SearchService.SearchResultComparer())
                        .Take(5)
                        .ToList();
            Console.WriteLine($"making response {watch.Elapsed} total: {System.DateTime.Now - data.Created}");
            if (orderedResult.Count() == 0)
                maxAge = A_MINUTE;
            var elapsed = watch.Elapsed;
            Task.Run(() =>
            {
                if (!(data is Server.ProxyMessageData<string, object>))
                    TrackingService.Instance.TrackSearch(data, data.Data, orderedResult.Count, elapsed);
            }).ConfigureAwait(false);
            return data.SendBack(data.Create(Type, orderedResult, maxAge));
        }

        private static void DequeueResult(Task<ConcurrentQueue<SearchService.SearchResultItem>> results, ConcurrentBag<SearchService.SearchResultItem> result)
        {
            while (results.Result.TryDequeue(out SearchService.SearchResultItem r))
                result.Add(r);
        }

        private async Task LoadPreview(Stopwatch watch, SearchService.SearchResultItem r)
        {
            try
            {
                PreviewService.Preview preview = null;
                if (r.Type == "player")
                    preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("pPrev", r.Id);
                else if (r.Type == "item")
                    preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("iPrev", r.Id.Split('?').First());

                if (preview == null)
                    return;

                r.Image = preview.Image;
                r.IconUrl = preview.ImageUrl;
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Failed to load preview for " + r.Id);
            }


        }
    }
}