namespace hypixel
{
    public class SetGoogleIdCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var id = UserService.Instance.GetOrCreateUser(data.GetAs<string>());
            data.Connection.UserId = id.Id;
        }
    }
}