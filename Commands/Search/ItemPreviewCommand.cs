namespace hypixel
{
    public class ItemPreviewCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.SendBack(data.Create("preview",
                        PreviewService.Instance.GetItemPreview(data.GetAs<string>()),
                        A_DAY));
        }
    }
}