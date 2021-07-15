using System.Threading.Tasks;

namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override Task Execute(MessageData data)
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
            return data.Ok();
        }
    }
}