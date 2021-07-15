using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Coflnet;

namespace hypixel
{
    public class ClearCacheCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var token = data.GetAs<string>();
            if(!FileController.Exists("authToken"))
            {
                if(token == "generate")
                {
                    // there is no token and we should generate one
                    var csp = new RNGCryptoServiceProvider();
                    var generatedToken = new byte[12];
                    csp.GetBytes(generatedToken);
                    FileController.SaveAs("authToken",Convert.ToBase64String(generatedToken));
                    return Task.CompletedTask;
                }
                throw new CoflnetException("error","There is no file called `authToken` in the data folder, please create one");
            }
            // make sure the token is valid local
            if(FileController.LoadAs<string>("authToken") != token)
            {
                throw new CoflnetException("error","The provided token was invalid, try again or just give up :)");
            }
            StorageManager.ClearCache();
            ItemDetails.Instance.Items = null;
            return Task.CompletedTask;
        }
    }
}
