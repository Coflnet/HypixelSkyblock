namespace hypixel
{
    public class PlayerNameCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var respone = CreateResponse(data, data.GetAs<string>());
            data.SendBack(respone);
        }

        public static MessageData CreateResponse(MessageData data, string uuid)
        {
            var name = PlayerSearch.Instance.GetName(uuid);
            return data.Create("nameResponse",name,A_WEEK);
        }
    }
}