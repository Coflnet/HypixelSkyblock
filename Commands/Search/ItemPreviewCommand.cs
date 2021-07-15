using System.Threading.Tasks;

namespace hypixel
{
    public class ItemPreviewCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(data.Create("preview",
                        PreviewService.Instance.GetItemPreview(data.GetAs<string>()),
                        A_DAY));
        }
    }
}