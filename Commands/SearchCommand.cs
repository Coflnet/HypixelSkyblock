using System.Linq;
using System.Text.RegularExpressions;

namespace hypixel {
    public class SearchCommand : Command {
        public override void Execute (MessageData data) {
            Regex rgx = new Regex ("[^a-zA-Z0-9_]");
            var search = rgx.Replace (data.Data, "").ToLower ();

            using (var context = new HypixelContext ()) {

                var result = context.Players
                    .Where (e => e.Name.ToLower ().StartsWith (search))
                    .OrderBy (p => p.Name.Length)
                    .Take (5)
                    .Select(p=>new PlayerResult(p.Name,p.UuId))
                    .ToList ();
                data.SendBack (MessageData.Create ("searchResponse", result));
            }

        }
    }
}