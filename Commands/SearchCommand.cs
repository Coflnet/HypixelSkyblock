using System.Linq;
using System.Text.RegularExpressions;

namespace hypixel
{
    public class SearchCommand : Command
    {
        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9_]");
            var search = rgx.Replace(data.Data, "").ToLower();
            var result = PlayerSearch.Instance.GetPlayers(search).Where(e=>e.Name.ToLower().StartsWith(search)).Take(5).ToList();
            data.SendBack(MessageData.Create("searchResponse",result));
        }
    }
}
