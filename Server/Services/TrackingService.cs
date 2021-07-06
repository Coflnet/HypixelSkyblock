using RestSharp;

namespace hypixel
{
    public class TrackingService
    {
        RestClient trackClient = new RestClient("https://track.coflnet.com");
        public static TrackingService Instance { get; protected set; }
        static TrackingService()
        {
            Instance = new TrackingService();
        }

        public void TrackSearch(MessageData data, string value, int resultCount)
        {
            trackClient.Execute(new RestRequest("/matomo.php?idsite=2&rec=1&action_name=search")
                    .AddQueryParameter("search", value)
                    .AddQueryParameter("search_count", resultCount.ToString()));
        }

        public void TrackPage(string url, string title, string referer)
        {
            trackClient.Execute(new RestRequest("/matomo.php?idsite=2&rec=1")
                    .AddQueryParameter("action_name", title)
                    .AddQueryParameter("url", url)
                    .AddQueryParameter("urlref", referer));
        }
    }
}