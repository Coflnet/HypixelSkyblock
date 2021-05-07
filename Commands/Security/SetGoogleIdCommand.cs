
using System;
using Google.Apis.Auth;
using Newtonsoft.Json;

namespace hypixel
{
    public class SetGoogleIdCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var token = ValidateToken(data.GetAs<string>());

            var id = UserService.Instance.GetOrCreateUser(token.Subject,token.Email);
            data.UserId = id.Id;
            data.Ok();
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