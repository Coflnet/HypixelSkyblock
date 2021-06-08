using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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
    }
}