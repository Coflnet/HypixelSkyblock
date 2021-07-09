using System;
using System.Linq;
using System.Runtime.Serialization;
using RestSharp;

namespace hypixel
{
    public class PreviewService 
    {
        public static PreviewService Instance;
        private RestClient crafatarClient = new RestClient("https://crafatar.com");
        private RestClient skyLeaClient = new RestClient("https://sky.lea.moe");
        private RestClient skyClient = new RestClient("https://sky.coflnet.com");
        private RestClient proxyClient = new RestClient("http://imgproxy");
        static PreviewService()
        {
            Instance = new PreviewService();
        }

        public Preview GetPlayerPreview(string id)
        {
            var request = new RestRequest("/avatars/{uuid}").AddUrlSegment("uuid",id).AddQueryParameter("overlay","");

            var uri = crafatarClient.BuildUri(request.AddParameter("size",64));
            var response = crafatarClient.DownloadData(request.AddParameter("size", 8));

            return new Preview()
            {
                Id = id,
                Image = Convert.ToBase64String(response),
                ImageUrl = uri.ToString(),
                Name = PlayerSearch.Instance.GetName(id)
            };
        }

        public Preview GetItemPreview(string tag)
        {
            var request = new RestRequest("/item/{tag}").AddUrlSegment("tag", tag);

            var uri = skyLeaClient.BuildUri(request);
            var detailsRequest = ItemDetails.Instance.GetDetailsWithCache(tag);
            IRestResponse response = GetProxied(uri);
            detailsRequest.Wait();
            var details = detailsRequest.Result;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                uri = skyClient.BuildUri(new RestRequest(details.IconUrl));
                response = GetProxied(uri);
            }

            return new Preview()
            {
                Id = tag,
                Image = Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri.ToString(),
                Name = details.Names.FirstOrDefault()
            };
        }

        private IRestResponse GetProxied(Uri uri)
        {
            var proxyRequest = new RestRequest("/x32/"+uri.ToString())
                        .AddUrlSegment("size", 32);
            var response = proxyClient.Execute(proxyRequest);
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
        }
    }
}