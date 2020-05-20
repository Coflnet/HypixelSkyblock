using dev;
using Newtonsoft.Json;

namespace hypixel
{
    public class BazaarPricesCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.SendBack(new MessageData("bazaarResponse",JsonConvert.SerializeObject(BazaarController.Instance.GetInfo(data.Data))));
        }
    }
}
