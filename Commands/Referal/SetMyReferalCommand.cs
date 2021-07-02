namespace hypixel
{
    public class SetMyReferalCommand : Command
    {
        public override void Execute(MessageData data)
        {
            ReferalService.Instance.WasReferedBy(data.User, data.GetAs<string>());
        }
    }
}
