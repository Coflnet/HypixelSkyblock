using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RestSharp.Extensions;

namespace hypixel
{
    public class UserService
    {
        public static UserService Instance { get; }
        static UserService()
        {
            Instance = new UserService();
        }

        internal GoogleUser GetOrCreateUser(string googleId,string email = null)
        {
            using (var context = new HypixelContext())
            {
                var user = context.Users.Where(u => u.GoogleId == googleId).FirstOrDefault();
                if (user == null)
                {
                    user = new GoogleUser()
                    {
                        GoogleId = googleId,
                        Email = email,
                        CreatedAt = DateTime.Now
                    };
                    context.Users.Add(user);
                    context.SaveChanges();
                }
                if(user.Email == null)
                {
                    user.Email = email;
                    context.SaveChanges();
                }

                return user;
            }
        }

        internal GoogleUser GetUserById(int userId)
        {
            using (var context = new HypixelContext())
            {
                var user = context.Users.Include(u=>u.Devices).Where(u=>u.Id == userId).FirstOrDefault();
                if (user == null)
                    throw new UserNotFoundException(userId.ToString());
                return user;
            }
        }

        public GoogleUser GetUser(string id)
        {
            using (var context = new HypixelContext())
            {
                var user = context.Users.Where(u => u.GoogleId == id).FirstOrDefault();
                if (user == null)
                    throw new UserNotFoundException(id);
                return user;
            }
        }

        public Task<List<Bonus>> GetBoni(int userId)
        {
            using(var context = new HypixelContext())
            {
                return context.Boni.Where(b => b.UserId == userId).ToListAsync();
            }
        }

        public void SavePurchase(GoogleUser user, int days, string transactionId)
        {
            using (var context = new HypixelContext())
            {
                Server.AddPremiumTime(days, user);
                context.Add(new Bonus()
                {
                    BonusTime = TimeSpan.FromDays(days),
                    ReferenceData = transactionId,
                    Type = Bonus.BonusType.PURCHASE,
                    UserId = user.Id
                });
                if (user.ReferedBy != 0)
                    context.Add(new Bonus()
                    {
                        BonusTime = TimeSpan.FromDays(days) / 10,
                        ReferenceData = transactionId,
                        Type = Bonus.BonusType.REFERED_UPGRADE,
                        UserId = user.ReferedBy
                    });
                context.Update(user); 
                context.SaveChanges();
            }
        }
    }

    public class Bonus
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public BonusType Type { get; set; }
        public TimeSpan BonusTime { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public string ReferenceData { get; set; }

        public enum BonusType
        {
            REFERAL,
            BEING_REFERED,
            FEEDBACK,
            /// <summary>
            /// A refered user upgraded to a premium plan
            /// </summary>
            REFERED_UPGRADE,
            PURCHASE
        }
    }
}