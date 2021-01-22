namespace hypixel
{
    public class PlayerNameCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var name = PlayerSearch.Instance.GetName(data.GetAs<string>());
            var respone = MessageData.Create("nameResponse",name,A_WEEK);
            data.SendBack(respone);
        }
    }
}