using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            Regex rgx = new Regex("[^a-zA-Z0-9_\\. ]");
            var search = rgx.Replace(data.Data, "").ToLower();
            var task = SearchService.Instance.Search(search);
            task.Wait();
            var result = task.Result;

            var tasks = new List<Task>();
            foreach (var r in result)
            {
                tasks.Add(Task.Run(async () =>
                {
                    PreviewService.Preview preview = null;
                    if (r.Type == "player")
                        preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("pPrev", r.Id);
                    else if (r.Type == "item")
                        preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("iPrev", r.Id);

                    if (preview == null)
                        return;
                    r.Image = preview.Image;
                    r.IconUrl = preview.ImageUrl;
                }));
            }
            var maxAge = A_DAY / 2;
            if (result.Count <= 3)
                // looks like a specific search, very unlikely to change 
                maxAge = A_DAY;

            if (Task.WaitAll(tasks.ToArray(), 50))
                System.Console.WriteLine("could await all");
            else
                maxAge = A_HOUR /2;
            

            if(result.Count == 0)
                maxAge = A_MINUTE;

            data.SendBack(data.Create(Type, result, maxAge));
        }
    }
}