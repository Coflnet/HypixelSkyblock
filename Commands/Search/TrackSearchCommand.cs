using System.Threading.Tasks;
using MessagePack;

namespace hypixel
{
    public class TrackSearchCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var hit = data.GetAs<Request>();
            if(hit.Type=="player" && hit.Id.Length == 32)
                PlayerSearch.Instance.AddHitFor(hit.Id);
            else 
                ItemDetails.Instance.AddHitFor(hit.Id);

            SearchService.Instance.AddPopularSite(hit.Type,hit.Id);
            return data.Ok();
            
            TrackingService.Instance.TrackPage($"http://sky.coflnet.com/{hit.Type}/{hit.Id}",$"{hit.Type}/{hit.Id}",data);
            return Task.CompletedTask;
        }
        [MessagePackObject]
        public class Request
        {
            [Key("type")]
            public string Type;
            [Key("id")]
            public string Id;
        }
    }
}