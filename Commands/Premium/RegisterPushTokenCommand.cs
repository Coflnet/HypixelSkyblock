using System.Threading.Tasks;
using MessagePack;

namespace hypixel
{
    public class RegisterPushTokenCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();
            NotificationService.Instance.AddToken(data.UserId, args.deviceName, args.token);
            return Task.CompletedTask;
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("name")]
            public string deviceName;
            [Key("token")]
            public string token;
        }
    }
}