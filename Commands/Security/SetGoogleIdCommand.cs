
using System;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Newtonsoft.Json;
using Prometheus;

namespace hypixel
{
    public class SetGoogleIdCommand : Command
    {
        Counter loginCount = Metrics.CreateCounter("loginCount", "How often the login was executed (with a googleid)");
        public override Task Execute(MessageData data)
        {
            var token = ValidateToken(data.GetAs<string>());

            var id = UserService.Instance.GetOrCreateUser(token.Subject,token.Email);
            data.UserId = id.Id;
            loginCount.Inc();
            return data.Ok();
        }

        public static GoogleJsonWebSignature.Payload ValidateToken(string token)
        {
            try
            {
                var client = GoogleJsonWebSignature.ValidateAsync(token);
                client.Wait();
                var tokenData = client.Result;
                Console.WriteLine("google user: " + tokenData.Name);
                return tokenData;
            } catch(Exception e)
            {
                throw new CoflnetException("invalid_token",$"{e.InnerException.Message}");
            }


        }
    }
}