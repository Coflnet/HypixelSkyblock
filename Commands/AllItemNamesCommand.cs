using Newtonsoft.Json;

namespace hypixel
{
    public class AllItemNamesCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.SendBack(new MessageData("itemNamesResponse",JsonConvert.SerializeObject(ItemDetails.Instance.AllItemNames())));
        }
    }
}
