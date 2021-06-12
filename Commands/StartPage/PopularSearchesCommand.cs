using System.Linq;

namespace hypixel
{
    public class PopularSearchesCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var pages = SearchService.Instance.GetPopularSites().Take(50).ToList();
            if(pages.Count < 10)
            {
                using(var context = new HypixelContext())
                {
                    pages = context.Items
                        .OrderByDescending(i => i.HitCount)
                        .Select(i=>new {Name=i.Names.FirstOrDefault(),i.Tag})
                        .Take(40).ToList()
                        .Select(i => new PopularSite(i.Name, "/item/" + i.Tag))
                        .ToList();
                }
            }
            data.SendBack(data.Create("popularSearches", pages, A_MINUTE * 5));

        }
    }
}
