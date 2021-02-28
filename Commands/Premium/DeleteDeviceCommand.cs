using System.Linq;

namespace hypixel
{
    public class DeleteDeviceCommand : Command
    {
        public async override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var name = data.GetAs<string>();

                var device = data.User.Devices.Where(d => d.Name == name).FirstOrDefault();
                if (device != null)
                    context.Remove(device);
                await context.SaveChangesAsync();
                data.Ok();
            }
        }
    }
}