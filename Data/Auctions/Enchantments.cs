using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hypixel
{
    [MessagePack.MessagePackObject]
    public class Enchantment
    {
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
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
             // invalid enchant, may be reasigned
            replenish = 60,
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
            counter_strike,
            turbo_carrot,
            turbo_cactus,
            turbo_cane,
            turbo_coco,
            turbo_melon,
            turbo_mushrooms,
            turbo_pumpkin,
            turbo_potato,
            turbo_warts,
            turbo_wheat,
            chance,
            PROSECUTE,
            syphon,
            respite,
            thunderbolt,
            titan_killer,
            triple_strike,
            ultimate_soul_eater,
            ultimate_one_for_all,
            None,
            cultivating,
            delicate,
            mana_steal,
            smarty_pants,
            pristine,
            
            Any = 126
        }

        [System.ComponentModel.DataAnnotations.Key]
        [MessagePack.IgnoreMember]
        [JsonIgnore]
        public int Id {get;set;}

        [MessagePack.Key(0)]
        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(3)")]
        public EnchantmentType Type {get;set;}

        [MessagePack.Key(1)]
        [JsonProperty("level")]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(3)")]
        public byte Level {get;set;}
        /// <summary>
        /// ItemType is here for faster sorting
        /// </summary>
        /// <value>The ItemType this enchantment coresponds to</value>
        [MessagePack.IgnoreMember]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "MEDIUMINT(9)")]
        [JsonIgnore]
        public int ItemType {get;set;}

        /// <summary>
        /// The id of the auctions this coresponds to
        /// </summary>
        /// <value></value>
        [MessagePack.IgnoreMember]
        [JsonIgnore]
        public int SaveAuctionId {get;set;}


        public Enchantment(EnchantmentType type, byte level,int itemType = 0)
        {
            Type = type;
            Level = level;
            ItemType = itemType;
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