namespace hypixel
{
    public class SetConnectionIdCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var socketData = data as SocketMessageData;
            if(socketData == null)
                throw new CoflnetException("invalid_command","this command can only be called by a socket connection");
            socketData.Connection.SetConnectionId(data.GetAs<string>());
        }
    }
}