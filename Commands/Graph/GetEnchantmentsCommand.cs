using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace hypixel
{
    public class GetEnchantmentsCommand : FilterOptionsCommand
    {
        public override Task Execute(MessageData data)
        {
            var values = Enum.GetValues(typeof(Enchantment.EnchantmentType))
                    .Cast<Enchantment.EnchantmentType>()
                    //.Where(ench => ench != Enchantment.EnchantmentType.unknown)
                    .Select(ench => new Formatted(ench.ToString(), (int)ench))
                    .ToList();

            return data.SendBack(new MessageData("getEnchantmentsResponse",
                JsonConvert.SerializeObject(values),
                A_DAY
            ));
        }
    }
}


