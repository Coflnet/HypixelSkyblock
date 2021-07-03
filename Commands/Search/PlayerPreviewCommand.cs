namespace hypixel
{
    public class PlayerPreviewCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.SendBack(data.Create("preview",
                        PreviewService.Instance.GetPlayerPreview(data.GetAs<string>()),
                        A_WEEK/2));
        }
    }
}