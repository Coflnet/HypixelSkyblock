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
            bane_of_anthropods,
            fire_aspect,
            looting,
            knockback,
            sharpness,
            smite,
            aqua_affinity,
            blast_protection,
            death_strider,
            feather_falling,
            fire_protection,
            frost_walker,
            projectile_protection,
            protection,
            raspiration,
            thorns,
            flame,
            power,
            punch,
            efficiency,
            fortune,
            silk_touch,
            lure,
            luck_of_the_sea,
            true_protection
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
    }
}