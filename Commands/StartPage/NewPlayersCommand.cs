using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace hypixel
{
    public class NewPlayersCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var players = context.Players
                    .OrderByDescending(p => p.UpdatedAt)
                    .Take(50)
                    .ToList()
                    .Select(p=>new Response(){Name = p.Name, UpdatedAt = p.UpdatedAt, UuId = p.UuId});
                return data.SendBack(data.Create("newPlayersResponse", players, A_MINUTE ));
            }
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "uuid")]
            public string UuId;
            [DataMember(Name = "time")]
            public DateTime UpdatedAt;
        }
    }
}
