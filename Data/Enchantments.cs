using System;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class Enchantment
    {
        public enum EnchantmentType
        {
            unknown,
            cleave,
            critical,
            cubism,
            ender_slayer,
            execute,
            experience,
            first_strike,
            giant_killer,
            impaling,
            lethality,
            life_steal,
            luck,
            scavenger,
            thunderlord,
            telekinesis,
            vampirism,
            venomous,
            growth,
            aiming,
            dragon_hunter,
            infinite_quiver,
            piercing,
            snipe,
            harvesting,
            rainbow,
            smelting_touch,
            angler,
            blessing,
            caster,
            frail,
            magnet,
            spiked_hook,
            bane_of_arthropods,
            fire_aspect,
            looting,
            knockback,
            sharpness,
            smite,
            aqua_affinity,
            blast_protection,
            depth_strider,
            feather_falling,
            fire_protection,
            frost_walker,
            projectile_protection,
            protection,
            respiration,
            thorns,
            flame,
            power,
            punch,
            efficiency,
            fortune,
            silk_touch,
            lure,
            luck_of_the_sea,
            true_protection,
            sugar_rush,
            bane_of_arthropod
            
        }

        [Key(0)]
        public EnchantmentType Type;
        [Key(1)]
        public short Level;

        public Enchantment(EnchantmentType type, short level)
        {
            Type = type;
            Level = level;
        }

        public Enchantment() {

        }

        public override bool Equals(object obj)
        {
            return obj is Enchantment enchantment &&
                   Type == enchantment.Type &&
                   Level == enchantment.Level;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Level);
        }
    }
}