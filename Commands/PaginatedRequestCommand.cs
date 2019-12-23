using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public abstract class PaginatedRequestCommand<T> : Command
    {
        public override void Execute(MessageData data)
        {
            var request = data.GetAs<Request>();
            


            var result = GetResult(request.Uuid,request.Amount,request.Offset);
            data.SendBack(MessageData.Create(ResponseCommandName,result));
        }

        private List<T> GetResult(string uuid, int amount, int offset)
        {
            var ids = GetAllIds(uuid);
            var count = ids.Count();
            List<T> result = new List<T>();

            var availableAmount = count - offset;
            if(availableAmount < amount)
            {
                amount = availableAmount;
            }
            if(amount <= 0)
            {
                // there are not enough
                return result;
            }

            return ids.Reverse()
                        .Skip(offset)
                        .Take(amount)
                        .Select(id=>GetElement(id))
                        .ToList();
        }

        public abstract IEnumerable<string> GetAllIds(string id);

        public abstract T GetElement(string id);

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
