using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hypixel
{
    /// <summary>
    /// Finds icons for given query
    /// </summary>
    public class IconResolver 
    {
        public static IconResolver Instance { get; }

        static IconResolver()
        {
            Instance = new IconResolver();
        }

        /// <summary>
        /// Find and returns item icon
        /// </summary>
        /// <param name="context"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task Resolve(Server.RequestContext context, string path)
        {
            var tag = path.Split("/").Last();
            var key = "img" + tag;
            var preview = await CacheService.Instance.GetFromRedis<PreviewService.Preview>(key);
            var cacheTime = TimeSpan.FromDays(1);
            Task save = null;
            if(preview == null)
            {
                if(!ItemDetails.Instance.TagLookup.ContainsKey(tag))
                    throw new CoflnetException("unkown_item", "The requested item was not found, please file a bugreport");
                preview = PreviewService.Instance.GetItemPreview(tag,64);
                if(preview.Image == "cmVxdWVzdGVkIFVSTCBpcyBub3QgYWxsb3dlZAo=" || preview.Image == null)
                {
                    // transparent 64x64 image
                    preview.Image = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAQAAAAAYLlVAAAAOUlEQVR42u3OIQEAAAACIP1/2hkWWEBzVgEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAYF3YDicAEE8VTiYAAAAAElFTkSuQmCC";
                    preview.MimeType = "image/png";
                    cacheTime = TimeSpan.FromMinutes(1);
                    TrackingService.Instance.TrackPage("https://error" + path, "not found/" + tag, null, null);
                }
                save = CacheService.Instance.SaveInRedis<PreviewService.Preview>(key,preview,cacheTime);
            }
            context.SetContentType(preview.MimeType);
            context.WriteAsync(Convert.FromBase64String(preview.Image));
            if(save != null)
                await save;
        }
    }
}
