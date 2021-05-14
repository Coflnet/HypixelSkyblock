using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace hypixel
{
    public class ItemDetailsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            string search = ReplaceInvalidCharacters(data.Data); // rgx.Replace(data.Data, "");
            data.SendBack(CreateResponse(search));
        }

        public static MessageData CreateResponse(string search)
        {
            var details = ItemDetails.Instance.GetDetails(search);
            var time = A_WEEK;
            if(details.Tag == "Unknown")
                time = 0;
            return new MessageData("itemDetailsResponse", JsonConvert.SerializeObject(details), time);
        }

        public static string ReplaceInvalidCharacters(string data)
        {
            Regex rgx = new Regex("[^a-zA-Z -\\[\\]]");
            var search = data.Replace("\"", "");
            return search;
        }
    }
}
