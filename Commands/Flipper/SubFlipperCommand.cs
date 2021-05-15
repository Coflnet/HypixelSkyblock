namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            Flipper.FlipperEngine.Instance.AddConnection(con);
            data.Ok();
        }
    }
}