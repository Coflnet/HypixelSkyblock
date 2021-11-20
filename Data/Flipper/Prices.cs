using System;
using System.Collections.Generic;
using System.Linq;
using hypixel;

namespace Coflnet.Sky
{
    public class Constants
    {
        // Godly on armor
        // toolsmith 
        // precise
        // Renowned
        // Treacherous
        // lucky on fishing rods
        // Spiritual about a mil
        // Silky, shaded on  talisman
        // fleet, Auspicious from reforge ..
        // fruitful, blessed
        // submerged, withered, stellar (picaxe/dril), lucky
        // ambered
        public readonly static HashSet<ItemReferences.Reforge> RelevantReforges = new HashSet<ItemReferences.Reforge>()
        {
            ItemReferences.Reforge.ancient,
            ItemReferences.Reforge.Necrotic,
            ItemReferences.Reforge.Gilded,
            ItemReferences.Reforge.withered,
            ItemReferences.Reforge.Spiritual,
            ItemReferences.Reforge.jaded, // (sorrow armor and divan, maybe just above some tier)
            ItemReferences.Reforge.warped, // for aote
            ItemReferences.Reforge.toil,
            ItemReferences.Reforge.moil, // on axe
            ItemReferences.Reforge.Fabled,
            ItemReferences.Reforge.Giant,
            ItemReferences.Reforge.submerged, // for shark armor
            ItemReferences.Reforge.Renowned, // for superior, sorrow armor
        };
        // include pet items lucky clover, shemlet, quick cloth, golden cloth, buble gum, text book
        // include gemstone (just add the bazaar price)
        // include scrolls

        public static HashSet<Enchantment> RelevantEnchants = new HashSet<Enchantment>()
        {
            new Enchantment(Enchantment.EnchantmentType.experience,4),
            new Enchantment(Enchantment.EnchantmentType.first_strike,5),
            new Enchantment(Enchantment.EnchantmentType.life_steal,4),
            new Enchantment(Enchantment.EnchantmentType.looting,4),
            new Enchantment(Enchantment.EnchantmentType.scavenger,4),
            new Enchantment(Enchantment.EnchantmentType.syphon,4),
            new Enchantment(Enchantment.EnchantmentType.vicious,1),
            new Enchantment(Enchantment.EnchantmentType.chance,4),
            new Enchantment(Enchantment.EnchantmentType.dragon_hunter,1),
            new Enchantment(Enchantment.EnchantmentType.snipe,4),
            new Enchantment(Enchantment.EnchantmentType.pristine,2), // maybe 1 as well
            new Enchantment(Enchantment.EnchantmentType.overload,2),
            new Enchantment(Enchantment.EnchantmentType.true_protection,1)
        };

        static Constants()
        {
            foreach (var item in Enum.GetValues(typeof(Enchantment.EnchantmentType)).Cast<Enchantment.EnchantmentType>())
            {
                if (item.ToString().StartsWith("ultimate_", true, null))
                {
                    RelevantEnchants.Add(new Enchantment(item,1));
                }
            }
        }
    }
}