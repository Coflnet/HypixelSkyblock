namespace hypixel
{
    public class UnsubFlipperCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            Flipper.FlipperEngine.Instance.RemoveNonConnection(con);
        }
    }
}