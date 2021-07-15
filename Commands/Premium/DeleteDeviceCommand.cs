using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class DeleteDeviceCommand : Command
    {
        public async override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var name = data.GetAs<string>();

                var device = data.User.Devices.Where(d => d.Name == name).FirstOrDefault();
                if (device != null)
                    context.Remove(device);
                await context.SaveChangesAsync();
                await data.Ok();
            }
        }
    }
}