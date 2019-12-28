namespace hypixel
{
    public class GetVersionCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.SendBack(MessageData.Create("version",5));
        }
    }
}
