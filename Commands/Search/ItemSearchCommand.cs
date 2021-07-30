using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace hypixel
{
    public class ItemSearchCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var search = RemoveInvalidChars(data.Data);
            var result = await ItemDetails.Instance.Search(search);
            await data.SendBack(data.Create("itemSearch", result.Select(a => new SearchService.SearchResultItem(a)).ToList(),A_HOUR));
        }

        static Regex rgx = new Regex("[^-a-zA-Z0-9_\\.' ]");
        public static string RemoveInvalidChars(string search)
        {
            return rgx.Replace(search, "").ToLower().Trim();
        }
    }
}