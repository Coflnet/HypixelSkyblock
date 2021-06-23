using System;
using System.Linq;
using System.Runtime.Serialization;
using HashidsNet;

namespace hypixel
{
    public class ReferalService
    {
        public static ReferalService Instance { get; }
        Hashids hashids = new Hashids("simple salt");
        static ReferalService()
        {
            Instance = new ReferalService();
        }

        public void WasReferedBy(GoogleUser user, string referer)
        {
            if (user.ReferedBy != 0)
                throw new CoflnetException("already_refered", "You already have used a referal Link. You can only be refered once.");
            var id = hashids.Decode(referer)[0];
            using (var context = new HypixelContext())
            {
                user.ReferedBy = id;
                // give the user 'test' premium time
                Server.AddPremiumTime(1, user);
                context.Update(user);
                var referUser = context.Users.Where(u => u.Id == id).FirstOrDefault();
                if (referUser != null)
                {
                    // award referal bonus to user who refered
                    Server.AddPremiumTime(1, referUser);
                    context.Update(user);
                }
                context.SaveChanges();
            }
        }

        public ReeralInfo GetReferalInfo(GoogleUser user)
        {
            using (var context = new HypixelContext())
            {
                var referedUsers = context.Users.Where(u => u.ReferedBy == user.Id).ToList();
                var minDate = new DateTime(2020, 2, 2);
                var upgraded = referedUsers.Where(u => u.PremiumExpires > minDate).Count();
                var receivedTime = TimeSpan.FromDays(upgraded * 3 + referedUsers.Count);
                return new ReeralInfo()
                {
                    RefId = hashids.Encode(user.Id),
                    BougthPremium = upgraded,
                    ReceivedTime = receivedTime,
                    ReferCount = referedUsers.Count
                };
            }
        }

        [DataContract]
        public class ReeralInfo
        {
            [DataMember(Name = "refId")]
            public string RefId;
            [DataMember(Name = "count")]
            public int ReferCount;
            [DataMember(Name = "receivedTime")]
            public TimeSpan ReceivedTime;
            [DataMember(Name = "bougthPremium")]
            public int BougthPremium;
        }
    }
}