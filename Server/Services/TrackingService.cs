using System;
using RestSharp;

namespace hypixel
{
    public class TrackingService
    {
        RestClient trackClient = new RestClient("https://track.coflnet.com");
        public static TrackingService Instance { get; protected set; }

        private string visitorId;

        static TrackingService()
        {
            Instance = new TrackingService();
        }

        public void TrackSearch(MessageData data, string value, int resultCount, TimeSpan time)
        {
            if(value.Length <= 2)
                return; 
            var genMs = ((int)time.TotalMilliseconds).ToString();
            trackClient.ExecuteAsync(new RestRequest("/matomo.php?idsite=2&rec=1&action_name=search")
                    .AddQueryParameter("search", value)
                    .AddQueryParameter("search_count", resultCount.ToString())
                    .AddQueryParameter("ua", GetUserAgent(data))
                    .AddQueryParameter("pf_srv", genMs));
            trackClient.ExecuteAsync(new RestRequest("/matomo.php?idsite=2&rec=1&action_name=search")
                    .AddQueryParameter("ua", "search")
                    .AddQueryParameter("action_name", "search/" + value)
                    .AddQueryParameter("url", "http://s/search/" + value)
                    .AddQueryParameter("cid","1234567890abcdef")
                    .AddQueryParameter("pf_srv", genMs));

                    
            Console.WriteLine($"took {((int)time.TotalMilliseconds)}");
        }

        public void TrackPage(string url, string title, MessageData data)
        {
            string userAgent = GetUserAgent(data);
            TrackPage(url, title, null, userAgent);
        }

        private static string GetUserAgent(MessageData data)
        {
            var userAgent = "";
            if (data is HttpMessageData httpData)
            {
                if (httpData.context is Server.WebsocketRequestContext context)
                {
                    userAgent = context.UserAgent;
                }
            }
            if (data is SocketMessageData socketData)
            {
                userAgent = socketData.Connection.Context.Headers["User-Agent"];
            }

            return userAgent;
        }

        public void TrackPage(string url, string title, string referer, string userAgend = null, TimeSpan genTime = default(TimeSpan))
        {
            System.Console.WriteLine("tracking " + url);
            var request = new RestRequest("/matomo.php?idsite=2&rec=1")
                    .AddQueryParameter("action_name", title)
                    .AddQueryParameter("url", url)
                    .AddQueryParameter("urlref", referer)
                    .AddQueryParameter("ua", userAgend);
            if (referer != null && url.Substring(0, 10) != referer.Substring(0, 10))
            {
                request.AddQueryParameter("new_visit", "1");
            }
            if (genTime != default(TimeSpan))
                request.AddQueryParameter("pf_srv", ((int)genTime.TotalMilliseconds).ToString());
            trackClient.ExecuteAsync(request);
        }

        /// <summary>
        /// Tracks errors
        /// </summary>
        /// <param name="type"></param>
        internal void CommandError(string type)
        {
            var request = new RestRequest("/matomo.php?idsite=2&rec=1")
                    .AddQueryParameter("action_name", "error/" + type);
            trackClient.ExecuteAsync(request);
        }
    }
}