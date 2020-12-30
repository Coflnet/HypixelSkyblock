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
            var result = SearchService.Instance.Search(search);

            data.SendBack(MessageData.Create("searchResponse", result));

        }

    }
}