using System;
using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class PremiumExpirationCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                try
                {
                    var user = data.User;
                    return data.SendBack(data.Create("premiumExpiration", user?.PremiumExpires));
                } catch(Exception)
                {
                    // no premium
                    return data.SendBack(data.Create<string>("premiumExpiration", null));
                }
            }
        }
    }
}
