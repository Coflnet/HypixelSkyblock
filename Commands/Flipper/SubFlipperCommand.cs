namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            if(!data.User.HasPremium)
                throw new CoflnetException("no_premium","Please purchase Premium to access Fast Flipps");
            Flipper.FlipperEngine.Instance.AddConnection(con,(int)data.mId);
            data.Ok();
        }
    }
}