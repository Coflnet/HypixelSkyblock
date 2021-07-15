using System.Threading.Tasks;

namespace hypixel
{
    public class GetDeviceListCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var devices = data.User.Devices;
            return data.SendBack(data.Create("devices", devices));

        }
    }
}