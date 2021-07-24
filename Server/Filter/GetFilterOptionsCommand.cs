using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace hypixel.Filter
{
    public class GetFilterOptionsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var filter = ItemPrices.Instance.FilterEngine.GetFilter(data.GetAs<string>());
            return data.SendBack(data.Create("filterOptions",new FilterOptions(filter),A_DAY/2));
        }
    }
}
