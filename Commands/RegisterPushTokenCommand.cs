using MessagePack;

namespace hypixel
{
    public class RegisterPushTokenCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();
            NotificationService.Instance.AddToken(data.Connection.UserId, args.deviceName, args.token);
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