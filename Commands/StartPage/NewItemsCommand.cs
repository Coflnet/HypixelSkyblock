using System.Linq;
using System.Runtime.Serialization;

namespace hypixel
{
    public class NewItemsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var items = context.Items.OrderByDescending(p => p.Id)
                    .Select(i=>new {IconUrl = i.IconUrl,Name = i.Names.Where(n=>n.Name != null && n.Name != "null").FirstOrDefault(),Tag= i.Tag})
                    .Where(i=>i.Name != null)
                    .Take(50)
                    .ToList()
                    .Select(i=>new Response(){IconUrl = i.IconUrl,Name = i.Name.Name,Tag= i.Tag})
                    .ToList();
                data.SendBack(data.Create("newItemsResponse", items, A_HOUR));
            }
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "tag")]
            public string Tag;
            [DataMember(Name = "icon")]
            public string IconUrl;
        }
    }
}
