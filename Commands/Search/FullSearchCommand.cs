using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MessagePack;
using Newtonsoft.Json;

namespace hypixel
{
    public class FullSearchCommand : Command
    {

        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9_ ]");
            var search = rgx.Replace(data.Data, "").ToLower();
            var task = SearchService.Instance.Search(search);
            task.Wait();
            var result = task.Result;

            var maxAge = A_HOUR * 4;
            if(result.Count <= 3)
            {
                // looks like a specific search, very unlikely to change 
                maxAge = A_WEEK;
            }

            data.SendBack(MessageData.Create("searchResponse", result,maxAge));

        }

    }
}