using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace hypixel
{
    public class AccountInfoCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var user = data.User;
            var token = LoginExternalCommand.GenerateToken(user.Email);
            var mcName = "unkown";
            if (user.MinecraftUuid != null)
                mcName = await PlayerSearch.Instance.GetNameWithCacheAsync(user.MinecraftUuid);
            await data.SendBack(data.Create("acInfo", new Response(user.Email, token, user.MinecraftUuid, mcName)));
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name = "email")]
            public string Email;
            [DataMember(Name = "token")]
            public string Token;
            [DataMember(Name = "mcId")]
            public string MinecraftId;
            [DataMember(Name = "mcName")]
            public string MinecraftName;

            public Response()
            {
            }

            public Response(string email, string token, string minecraftId, string mcName)
            {
                Email = email;
                Token = token;
                MinecraftId = minecraftId;
                MinecraftName = mcName;
            }

            
        }
    }
}
