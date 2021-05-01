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
            return new MessageData("itemDetailsResponse", JsonConvert.SerializeObject(ItemDetails.Instance.GetDetails(search)), A_WEEK);
        }

        public static string ReplaceInvalidCharacters(string data)
        {
            Regex rgx = new Regex("[^a-zA-Z -\\[\\]]");
            var search = data.Replace("\"", "");
            return search;
        }
    }
}
