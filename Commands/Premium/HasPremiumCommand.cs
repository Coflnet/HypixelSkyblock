using System;
using System.Linq;

namespace hypixel
{
    public class PremiumExpirationCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var googleId = data.GetAs<string>();
            using(var context = new HypixelContext())
            {
                var user = context.Users.Where(u=>u.GoogleId == googleId).FirstOrDefault();
                data.SendBack(data.Create("premiumExpiration",user?.PremiumExpires));
            }
        }
    }
}
