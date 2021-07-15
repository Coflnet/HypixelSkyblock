using System.Threading.Tasks;
using Newtonsoft.Json;

namespace hypixel
{
    public class AllItemNamesCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(new MessageData("itemNamesResponse",JsonConvert.SerializeObject(ItemDetails.Instance.AllItemNames()),A_WEEK));
        }
    }
}
