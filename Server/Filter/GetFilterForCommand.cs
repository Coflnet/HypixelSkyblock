using System.Linq;
using System.Threading.Tasks;

namespace hypixel.Filter
{
    public class GetFilterForCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var details = await ItemDetails.Instance.GetDetailsWithCache(data.GetAs<string>());
            var filters = ItemPrices.Instance.FilterEngine.FiltersFor(details);
            await data.SendBack(data.Create("filterFor", filters.Select(f => new FilterOptions(f)).ToList(),A_DAY));
        }
    }
}
