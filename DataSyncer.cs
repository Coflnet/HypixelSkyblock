
using System;
using hypixel;
using Newtonsoft.Json;
using RestSharp;
/// <summary>
/// Responsible for syncing data
/// </summary>
public class DataSyncer
{

    public void Sync(string uuid = "a1d119e53dc647a88e4eb24b457fae16", string url = "https://auctions.craftlink.xyz/graphql")
    {
        var client = new RestClient(url);
        client.Timeout = -1;
        var request = new RestRequest(Method.POST);
        request.AddHeader("Content-Type", "application/json");
        request.AddParameter("application/json", "{\"query\":\"query Auction($id: String) {\\n  auction(id: $id) {\\n    id\\n    itemBytes\\n  }\\n}\\n\",\"variables\":{\"id\":\""+uuid+"\"}}",
                ParameterType.RequestBody);
        IRestResponse response = client.Execute(request);
        dynamic result = JsonConvert.DeserializeObject(response.Content);
        Console.WriteLine(result.data.auction.id);
        var a = StorageManager.GetOrCreateAuction((string)result.data.auction.id);
        Console.WriteLine(JsonConvert.SerializeObject(a));
        NBT.FillDetails(a,(string)result.data.auction.itemBytes);
        Console.WriteLine(JsonConvert.SerializeObject(a));
    }

}