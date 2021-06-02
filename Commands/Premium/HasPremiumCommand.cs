using System;
using System.Linq;

namespace hypixel
{
    public class PremiumExpirationCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                try
                {
                    var user = data.User;
                    data.SendBack(data.Create("premiumExpiration", user?.PremiumExpires));
                } catch(Exception)
                {
                    // no premium
                    data.SendBack(data.Create<string>("premiumExpiration", null));
                }
            }
        }
    }
}
