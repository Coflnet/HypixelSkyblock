using static hypixel.ItemReferences;
using System;
using MessagePack;
using System.Threading.Tasks;

namespace hypixel
{
    public class GetActiveAuctionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var details = data.GetAs<ActiveItemSearchQuery>();
            var count = 20;
            if(details.Limit < count && details.Limit > 0)
                count = details.Limit;

            var res = await ItemPrices.Instance.GetActiveAuctions(details);

            await data.SendBack(data.Create("activeAuctions", res, A_MINUTE * 2));
        }

        public enum SortOrder
        {
            RELEVANT = 0,
            HIGHEST_PRICE = 1,
            LOWEST_PRICE = 2,
            ENDING_SOON = 4
        } 

        [MessagePackObject]
        public class ActiveItemSearchQuery : ItemSearchQuery
        {
            [Key("order")]
            public SortOrder Order;
            [Key("limit")]
            public int Limit;
        }
    }
}