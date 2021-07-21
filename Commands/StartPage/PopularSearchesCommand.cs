using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace hypixel
{
    public class PopularSearchesCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var r = new System.Random();
            using (var context = new HypixelContext())
            {
                var pages = context.Items
                    .OrderByDescending(i => i.HitCount)
                    .Select(i => new { Name = i.Names.FirstOrDefault(), i.Tag, i.IconUrl })
                    .Take(40).ToList()
                    .Select(i => new Result() { title = i.Name?.Name, url = "/item/" + i.Tag, img = "/static/icon/"+i.Tag })
                    .ToList();


                pages.AddRange(context.Players
                    .OrderByDescending(i => i.HitCount).Select(i => new { Name = i.Name, i.UuId })
                    .Take(40)
                    .ToList()
                    .Select(p => new Result() { title = p.Name, url = "/player/" + p.UuId, img = SearchService.PlayerHeadUrl(p.UuId) }));

                return data.SendBack(data.Create("popularSearches", pages
                    .OrderBy(s => r.Next()).Take(50).ToList(), A_MINUTE * 5));
            }
        }

        [DataContract]
        public class Result
        {
            [DataMember]
            public string title;
            [DataMember]
            public string url;
            [DataMember]
            public string img;
        }
    }
}
