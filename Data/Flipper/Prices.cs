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
            // only 50k ItemReferences.Reforge.moil, // on axe
            ItemReferences.Reforge.Fabled,
            ItemReferences.Reforge.Giant,
            ItemReferences.Reforge.submerged, // for shark armor
            ItemReferences.Reforge.Renowned, // for superior, sorrow armor
        };
        // include pet items lucky clover, shemlet, quick clow, golden clow, buble gum, text book
        // include gemstone (just add the bazaar price)
        // include scrolls

        public static HashSet<Enchantment> RelevantEnchants = new HashSet<Enchantment>()
        {
            new Enchantment(Enchantment.EnchantmentType.first_strike,5),
            new Enchantment(Enchantment.EnchantmentType.triple_strike,5),
            new Enchantment(Enchantment.EnchantmentType.life_steal,5),
            new Enchantment(Enchantment.EnchantmentType.looting,5),
            new Enchantment(Enchantment.EnchantmentType.scavenger,5),
            new Enchantment(Enchantment.EnchantmentType.syphon,5),
            new Enchantment(Enchantment.EnchantmentType.vicious,1),
            new Enchantment(Enchantment.EnchantmentType.chance,5),
            new Enchantment(Enchantment.EnchantmentType.dragon_hunter,1),
            new Enchantment(Enchantment.EnchantmentType.snipe,4),
            new Enchantment(Enchantment.EnchantmentType.pristine,2), // maybe 1 as well
            new Enchantment(Enchantment.EnchantmentType.overload,2),
            new Enchantment(Enchantment.EnchantmentType.true_protection,1),
            new Enchantment(Enchantment.EnchantmentType.smite,7),
            new Enchantment(Enchantment.EnchantmentType.critical,7),
            new Enchantment(Enchantment.EnchantmentType.giant_killer,7),
            new Enchantment(Enchantment.EnchantmentType.luck,7),
            new Enchantment(Enchantment.EnchantmentType.angler,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.spiked_hook,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.caster,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.magnet,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.luck_of_the_sea,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.vampirism,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.thunderlord,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.lethality,7), // doesn't exist but generally worth nothing
            new Enchantment(Enchantment.EnchantmentType.infinite_quiver,11),
            new Enchantment(Enchantment.EnchantmentType.feather_falling,11),
            new Enchantment(Enchantment.EnchantmentType.ultimate_last_stand,3), // 1 and 2 are worth nothing,
            new Enchantment(Enchantment.EnchantmentType.ultimate_bank,6),
            new Enchantment(Enchantment.EnchantmentType.ultimate_combo,5),
            new Enchantment(Enchantment.EnchantmentType.ultimate_jerry,5),
            new Enchantment(Enchantment.EnchantmentType.ultimate_last_stand,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_no_pain_no_gain,5),
            new Enchantment(Enchantment.EnchantmentType.ultimate_rend,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_swarm,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_wise,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_wisdom,3),
            new Enchantment(Enchantment.EnchantmentType.compact,9)
        };

        static Constants()
        {
            foreach (var item in Enum.GetValues(typeof(Enchantment.EnchantmentType)).Cast<Enchantment.EnchantmentType>())
            {
                if (item.ToString().StartsWith("ultimate_", true, null))
                {
                    if (!RelevantEnchants.Where(e => e.Type == item).Any())
                        RelevantEnchants.Add(new Enchantment(item, 1));
                }
            }
        }
    }
}