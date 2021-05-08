namespace hypixel
{
    public class GetDeviceListCommand : Command
    {
        public override void Execute(MessageData data)
        {

            var devices = data.User.Devices;
            data.SendBack(data.Create("devices", devices));

        }
    }
}