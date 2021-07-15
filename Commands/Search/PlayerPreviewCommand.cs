using System.Threading.Tasks;

namespace hypixel
{
    public class PlayerPreviewCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(data.Create("preview",
                        PreviewService.Instance.GetPlayerPreview(data.GetAs<string>()),
                        A_WEEK/2));
        }
    }
}