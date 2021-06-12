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
                    .Take(50)
                    .Select(i=>new Response(){IconUrl = i.IconUrl,Name = i.Names.FirstOrDefault(),Tag= i.Tag})
                    .ToList();
                data.SendBack(data.Create("newItemsResponse", items, A_MINUTE));
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
