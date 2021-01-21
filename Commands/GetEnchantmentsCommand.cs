using System;
using System.Linq;
using Newtonsoft.Json;

namespace hypixel
{
    public class GetEnchantmentsCommand : FilterCommand
    {
        public override void Execute(MessageData data)
        {
            var values = Enum.GetValues(typeof(Enchantment.EnchantmentType))
                    .Cast<Enchantment.EnchantmentType>()
                    //.Where(ench => ench != Enchantment.EnchantmentType.unknown)
                    .Select(ench => new Formatted(ench.ToString(), (int)ench))
                    .ToList();

            data.SendBack(new MessageData("getEnchantmentsResponse",
                JsonConvert.SerializeObject(values),
                A_DAY
            ));
        }
    }
}


