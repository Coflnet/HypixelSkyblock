using System.Threading.Tasks;
using dev;
using Newtonsoft.Json;

namespace hypixel
{
    public class BazaarPricesCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(new MessageData("bazaarResponse",JsonConvert.SerializeObject(BazaarController.Instance.GetInfo(data.Data))));
        }
    }
}
