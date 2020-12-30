using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace hypixel
{
    public class ItemDetailsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z -]");
            var search = data.Data.Replace("\"",""); // rgx.Replace(data.Data, "");
            data.SendBack(new MessageData("itemDetailsResponse",JsonConvert.SerializeObject(ItemDetails.Instance.GetDetails(search))));
        }
    }
}
