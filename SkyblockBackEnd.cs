using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace hypixel
{
    public class SkyblockBackEnd : WebSocketBehavior
    {
        public static Dictionary<string,Command> Commands = new Dictionary<string, Command>();

        static SkyblockBackEnd()
        {
            Commands.Add("search",new SearchCommand());
            Commands.Add("itemPrices",new ItemPricesCommand());
            Commands.Add("playerDetails",new PlayerDetailsCommand());
            Commands.Add("version",new GetVersionCommand());
            Commands.Add("auctionDetails",new AuctionDetails());
            Commands.Add("itemDetails",new ItemDetailsCommand());
            Commands.Add("clearCache",new ClearCacheCommand());
            Commands.Add("playerAuctions",new PlayerAuctionsCommand());
            Commands.Add("playerBids",new PlayerBidsCommand());
            Commands.Add("allItemNames",new AllItemNamesCommand());
            Commands.Add("bazaarPrices",new BazaarPricesCommand());

            

            
        }

        protected override void OnMessage (MessageEventArgs e)
        {
            long mId = 0;
            try{
                var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                mId = data.mId;
                data.Connection = this;
                 data.Data = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data.Data));
                 Console.WriteLine(data.Data);

                if(!Commands.ContainsKey(data.Type))
                {
                    data.SendBack(new MessageData("error",$"The command `{data.Type}` is Unkown, please check your spelling"));
                    return;
                }
                Commands[data.Type].Execute(data);
            } catch(CoflnetException ex)
            {
                SendBack(new MessageData(ex.Slug,ex.Message){mId =mId});
            }catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                SendBack(new MessageData("error","The Message has to follow the format {\"type\":\"SomeType\",\"data\":\"\"}"){mId =mId});
               
                throw ex;
            }
            
            
        }

        public void SendBack(MessageData data)
        {
            Send(MessagePackSerializer.ToJson(data));
        }
    }
}
