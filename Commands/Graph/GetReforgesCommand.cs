using System;
using System.Linq;
using Newtonsoft.Json;

namespace hypixel
{
    public class GetReforgesCommand : FilterOptionsCommand
    {
        public override void Execute(MessageData data)
        {
            var values = Enum.GetValues(typeof(ItemReferences.Reforge))
                    .Cast<ItemReferences.Reforge>()
                    //.Where(ench => ench != Enchantment.EnchantmentType.unknown)
                    .SkipLast(1)
                    .Select(ench =>{ 
                        var intValue =  (int)ench;
                        // switch them
                        if(ench == ItemReferences.Reforge.Any)
                            intValue = (int)ItemReferences.Reforge.None;
                        else if(ench == ItemReferences.Reforge.None)
                            intValue = (int)ItemReferences.Reforge.Any;
                        return new Formatted(ench.ToString(),intValue);})
                    .ToList();

            data.SendBack(new MessageData("getReforgesResponse",
                JsonConvert.SerializeObject(values),
                A_DAY
            ));
        }
    }
}


