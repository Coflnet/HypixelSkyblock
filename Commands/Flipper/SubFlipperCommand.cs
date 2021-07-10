namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            try
            {

                if (!data.User.HasPremium)
                    Flipper.FlipperEngine.Instance.AddNonConnection(con, (int)data.mId);
                else
                {
                    Flipper.FlipperEngine.Instance.AddConnection(con, (int)data.mId);
                    Flipper.FlipperEngine.Instance.RemoveNonConnection(con);
                }
            }
            catch (CoflnetException)
            {
                Flipper.FlipperEngine.Instance.AddNonConnection(con, (int)data.mId);
            }
            data.Ok();
        }
    }
}