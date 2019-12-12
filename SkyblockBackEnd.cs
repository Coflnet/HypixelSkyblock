using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            Commands.Add("itemDetails",new ItemDetailsCommand());
            Commands.Add("version",new GetVersionCommand());

            
        }

        protected override void OnMessage (MessageEventArgs e)
        {
            try{
                var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                data.Connection = this;
                 data.Data = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data.Data));
                 Console.WriteLine(data.Data);

                if(!Commands.ContainsKey(data.Type))
                {
                    SendBack(new MessageData("error",$"The command `{data.Type}` is Unkown, please check your spelling"));
                    return;
                }
                Commands[data.Type].Execute(data);
            } catch(CoflnetException ex)
            {
                SendBack(new MessageData(ex.Slug,ex.Message));
            }catch (Exception ex)
            {
                SendBack(new MessageData("error","The Message has to follow the format {\"type\":\"SomeType\",\"data\":\"\"}"));
               
                throw ex;
            }
            
            
        }

        public void SendBack(MessageData data)
        {
            Send(MessagePackSerializer.ToJson(data));
        }
    }

    public abstract class Command
    {
        public abstract void Execute(MessageData data);
    }

    public class SearchCommand : Command
    {
        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9_]");
            var search = rgx.Replace(data.Data, "");
            var result = PlayerSearch.Instance.GetPlayers(search).Where(e=>e.Name.StartsWith(search)).Take(5).ToList();
            data.SendBack(MessageData.Create("searchResponse",result));
        }
    }

    public class GetVersionCommand : Command
    {
        public override void Execute(MessageData data)
        {
            data.SendBack(MessageData.Create("version",2));
        }
    }
}
