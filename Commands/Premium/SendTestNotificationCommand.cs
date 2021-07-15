using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class SendTestNotificationCommand : Command
    {
        public async override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var name = data.GetAs<string>();

                var device = data.User.Devices.Where(d => d.Name == name).FirstOrDefault();

                var notification = new NotificationService.Notification("Test notification", $"This is your device named '{device.Name}'", "https://sky.coflnet.com/devices", null, null, null);
                await NotificationService.Instance.TryNotifyAsync(device.Token, notification);
            }
        }
    }
}