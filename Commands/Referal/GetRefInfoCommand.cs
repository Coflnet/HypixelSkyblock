namespace hypixel
{
    public class GetRefInfoCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var refInfo = ReferalService.Instance.GetReferalInfo(data.User);
            data.SendBack(data.Create("refInfo", refInfo));
        }
    }
}
