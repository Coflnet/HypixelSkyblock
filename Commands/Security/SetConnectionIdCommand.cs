using System.Threading.Tasks;

namespace hypixel
{
    public class SetConnectionIdCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var socketData = data as SocketMessageData;
            if(socketData == null)
                throw new CoflnetException("invalid_command","this command can only be called by a socket connection");
            socketData.Connection.SetConnectionId(data.GetAs<string>());
            return data.Ok();
        }
    }
}