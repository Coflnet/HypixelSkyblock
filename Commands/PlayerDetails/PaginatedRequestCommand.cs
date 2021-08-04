using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;

namespace hypixel
{
    public abstract class PaginatedRequestCommand<T> : Command
    {
        public override Task Execute(MessageData data)
        {
            var request = data.GetAs<Request>();

            if(Program.LightClient && request.Offset > 0)
            {
                return ClientProxy.Instance.Proxy(data);
            }
            
            //PlayerSearch.Instance.AddHitFor(request.Uuid);

            var result = GetResult(request.Uuid,request.Amount,request.Offset);
            if(Program.LightClient && result.Count == 0)
            {
                return ClientProxy.Instance.Proxy(data);
            }
            return data.SendBack(data.Create(ResponseCommandName,result,A_MINUTE));
        }

        private List<T> GetResult(string uuid, int amount, int offset)
        {
            //var ids = GetAllIds(uuid);
            //var count = ids.Count();
 

            return GetAllElements(uuid,amount,offset).ToList();
        }

        public abstract IEnumerable<string> GetAllIds(string id);

        public abstract T GetElement(string id,string parentUuid);

        public abstract IEnumerable<T> GetAllElements(string selector,int offset,int amount);

        [MessagePackObject]
        public class Request
        {
            [Key("uuid")]
            public string Uuid;

            [Key("amount")]
            public int Amount;

            [Key("offset")]
            public int Offset;
        }

        public abstract string ResponseCommandName {get;}
    }
}
