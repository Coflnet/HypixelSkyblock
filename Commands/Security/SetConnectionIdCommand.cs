namespace hypixel
{
    public class SetConnectionIdCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.Connection.SetConnectionId(data.GetAs<string>());
        }
    }
}