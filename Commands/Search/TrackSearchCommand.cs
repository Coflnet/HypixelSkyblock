using MessagePack;

namespace hypixel
{
    public class TrackSearchCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var hit = data.GetAs<Request>();
            if(hit.Type=="player" && hit.Id.Length == 32)
                PlayerSearch.Instance.AddHitFor(hit.Id);
            else 
                ItemDetails.Instance.AddHitFor(hit.Id);

            SearchService.Instance.AddPopularSite(hit.Type,hit.Id);
            data.Ok();
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