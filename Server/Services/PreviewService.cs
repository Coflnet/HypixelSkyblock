using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using RestSharp;

namespace hypixel
{
    public class PreviewService 
    {
        public static PreviewService Instance;
        private RestClient crafatarClient = new RestClient("https://crafatar.com");
        private RestClient skyLeaClient = new RestClient("https://sky.shiiyu.moe");
        private RestClient skyClient = new RestClient("https://sky.coflnet.com");
        private RestClient proxyClient = new RestClient("http://imgproxy");
        static PreviewService()
        {
            Instance = new PreviewService();
        }

        public async Task<Preview> GetPlayerPreview(string id)
        {
            var request = new RestRequest("/avatars/{uuid}").AddUrlSegment("uuid",id).AddQueryParameter("overlay","");

            var uri = crafatarClient.BuildUri(request.AddParameter("size",64));
            var response = await crafatarClient.ExecuteAsync(request.AddParameter("size", 8));

            return new Preview()
            {
                Id = id,
                Image = response.RawBytes == null ? null : Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri.ToString(),
                Name = PlayerSearch.Instance.GetName(id)
            };
        }

        public async Task<Preview> GetItemPreview(string tag, int size = 32)
        {
            var request = new RestRequest("/item/{tag}").AddUrlSegment("tag", tag);

            var details = await ItemDetails.Instance.GetDetailsWithCache(tag);
            /* Most icons are currently available via the texture pack
            if(details.MinecraftType.StartsWith("Leather "))
                request = new RestRequest("/leather/{type}/{color}")
                    .AddUrlSegment("type", details.MinecraftType.Replace("Leather ","").ToLower())
                    .AddUrlSegment("color", details.color.Replace(":",",")); */

            var uri = skyLeaClient.BuildUri(request);
            IRestResponse response = await GetProxied(uri,size);
            


            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                uri = skyClient.BuildUri(new RestRequest(details.IconUrl));
                response = await GetProxied(uri,size);
            }

            return new Preview()
            {
                Id = tag,
                Image = Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri.ToString(),
                Name = details.Names.FirstOrDefault(),
                MimeType = response.ContentType
            };
        }

        private async Task<IRestResponse> GetProxied(Uri uri, int size)
        {
            var proxyRequest = new RestRequest($"/x{size}/"+uri.ToString())
                        .AddUrlSegment("size", size);
            var response = await proxyClient.ExecuteAsync(proxyRequest);
            return response;
        }

        [DataContract]
        public class Preview
        {
            [DataMember(Name ="id")]
            public string Id;
            [DataMember(Name ="img")]
            public string Image;
            [DataMember(Name ="name")]
            public string Name;
            [DataMember(Name ="imgUrl")]
            public string ImageUrl;
            [DataMember(Name ="mime")]
            public string MimeType;
        }
    }
}