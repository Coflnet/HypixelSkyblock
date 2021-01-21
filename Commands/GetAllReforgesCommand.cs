using System;
using System.Linq;
using Newtonsoft.Json;

namespace hypixel
{
    public class GetAllReforgesCommand : Command {
        public override void Execute (MessageData data) {
            var values = Enum.GetValues (typeof (ItemReferences.Reforge))
                    .Cast<ItemReferences.Reforge>()
                    .Select (ench => ench.ToString ())
                    .ToList ();

            data.SendBack (new MessageData ("getReforgesResponse",
                JsonConvert.SerializeObject (values),
                A_DAY
            ));
        }
    }
}