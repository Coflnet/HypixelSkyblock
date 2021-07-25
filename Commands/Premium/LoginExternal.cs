using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace hypixel
{
    public class LoginExternalCommand : Command
    {
        static string secret = SimplerConfig.Config.Instance["TOKEN_SECRET"];

        public override Task Execute(MessageData data)
        {
            var args = data.GetAs<Params>();
            var token = GenerateToken(args.Email);
            if (token != args.Token)
                throw new CoflnetException("invalid_token", "the token you passed was not valid");

            using (var context = new HypixelContext())
            {
                data.UserId = context.Users.Where(u => u.Email == args.Email).Select(u => u.Id).FirstOrDefault();
            }
            return data.SendBack(data.Create("login_success", "you were logged in"));
        }

        [DataContract]
        public class Params
        {
            [DataMember(Name = "email")]
            public string Email;
            [DataMember(Name = "token")]
            public string Token;
        }

        public static string GenerateToken(string email)
        {
            var bytes = Encoding.UTF8.GetBytes(email.ToLower() + secret);
            var hash = System.Security.Cryptography.SHA512.Create();
            return Convert.ToBase64String(hash.ComputeHash(bytes)).Truncate(20);
        }
    }
}
