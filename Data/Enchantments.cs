using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hypixel
{
    [MessagePack.MessagePackObject]
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
            bane_of_arthropod,
            replenish,
            rejuvenate,
            ultimate_bank,
            ultimate_combo,
            ultimate_jerry,
            ultimate_last_stand,
            ultimate_no_pain_no_gain,
            ultimate_wisdom,
            ultimate_wise,
            expertise,
            ultimate_chimera,
            ultimate_rend,
            overload,
            ultimate_legion,
            ultimate_swarm,
            big_brain,
            compact,
            vicious,
            counter_strike
            
        }

        [System.ComponentModel.DataAnnotations.Key]
        [MessagePack.IgnoreMember]
        [JsonIgnore]
        public int Id {get;set;}

        [MessagePack.Key(0)]
        [JsonConverter(typeof(StringEnumConverter))]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        public EnchantmentType Type {get;set;}

        [MessagePack.Key(1)]
        public short Level {get;set;}


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