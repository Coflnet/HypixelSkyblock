using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace hypixel
{
    public class ItemDetailsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(CreateResponse(data));
        }

        public static MessageData CreateResponse(MessageData data)
        {
            string search = ReplaceInvalidCharacters(data.Data); 
            var details = ItemDetails.Instance.GetDetails(search);
            var time = A_DAY;
            if(details.Tag == "Unknown" || string.IsNullOrEmpty(details.Tag))
                time = 0;
            return data.Create("itemDetailsResponse", details, time);
        }

        public static string ReplaceInvalidCharacters(string data)
        {
            Regex rgx = new Regex("[^a-zA-Z -\\[\\]]");
            var search = data.Replace("\"", "");
            return search;
        }
    }
}
