namespace hypixel
{
    public class PlayerNameCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var respone = CreateResponse(data.GetAs<string>());
            data.SendBack(respone);
        }

        public static MessageData CreateResponse(string uuid)
        {
            var name = PlayerSearch.Instance.GetName(uuid);
            return MessageData.Create("nameResponse",name,A_WEEK);
        }
    }
}