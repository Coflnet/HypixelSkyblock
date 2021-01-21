using System;
using System.Linq;
using Newtonsoft.Json;

namespace hypixel
{
    public class GetReforgesCommand : FilterCommand
    {
        public override void Execute(MessageData data)
        {
            var values = Enum.GetValues(typeof(ItemReferences.Reforge))
                    .Cast<ItemReferences.Reforge>()
                    //.Where(ench => ench != Enchantment.EnchantmentType.unknown)
                    .SkipLast(1)
                    .Select(ench => new Formatted(ench.ToString(), (int)ench))
                    .ToList();

            data.SendBack(new MessageData("getReforgesResponse",
                JsonConvert.SerializeObject(values),
                A_DAY
            ));
        }
    }
}


