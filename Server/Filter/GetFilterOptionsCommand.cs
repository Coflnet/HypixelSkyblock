namespace hypixel.Filter
{
    public class GetFilterOptionsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var filter = ItemPrices.Instance.FilterEngine.GetFilter(data.GetAs<string>());
            data.SendBack(data.Create("filterOptions",new FilterOptions(filter),A_DAY));
        }
    }
}
