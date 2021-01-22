using System;
using System.Linq;
using Newtonsoft.Json;

namespace hypixel
{
    public class GetAllEnchantmentsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var values = Enum.GetValues(typeof(Enchantment.EnchantmentType))
                    .Cast<Enchantment.EnchantmentType>()
                    .Where(ench => ench != Enchantment.EnchantmentType.unknown)
                    .Select(ench => ench.ToString())
                    .ToList();

            data.SendBack(new MessageData("getAllEnchantmentsResponse",
                JsonConvert.SerializeObject(values),
                A_DAY
            ));
        }
    }
}


