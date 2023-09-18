using System;
using System.Collections.Generic;
using System.Linq;

namespace Coflnet.Sky.Core;
/// <summary>
/// Maps properties to items to be able to get their cost
/// </summary>
public class PropertyMapper
{
    private Dictionary<(string, string), (List<string> needed, string previousLevel)> propertyToItem = new()
        {
            { ("upgrade_level", "10"), (new(){"FIFTH_MASTER_STAR"}, "9")},
            { ("upgrade_level", "9"), (new(){"FOURTH_MASTER_STAR"}, "8")},
            { ("upgrade_level", "8"), (new(){"THIRD_MASTER_STAR"}, "7")},
            { ("upgrade_level", "7"), (new(){"SECOND_MASTER_STAR"}, "6")},
            { ("upgrade_level", "6"), (new(){"FIRST_MASTER_STAR"}, string.Empty)},
            { ("rarity_upgrades", "1"), (new(){"RECOMBOBULATOR_3000"}, string.Empty)},
            { ("ethermerge", "1"), (new(){"ETHERWARP_CONDUIT", "ETHERWARP_MERGER"}, string.Empty)},
            { ("artOfPeaceApplied", "1"), (new(){"THE_ART_OF_PEACE"}, string.Empty)},
            { ("art_of_war_count", "1"), (new(){"THE_ART_OF_WAR"}, string.Empty)},
            { ("wood_singularity_count", "1"), (new(){"WOOD_SINGULARITY"}, string.Empty)},
            {("farming_for_dummies", "5"), (new(){"FARMING_FOR_DUMMIES"}, "4")},
            {("farming_for_dummies", "4"), (new(){"FARMING_FOR_DUMMIES"}, "3") },
            {("farming_for_dummies", "3"), (new(){"FARMING_FOR_DUMMIES"}, "2") },
            {("farming_for_dummies", "2"), (new(){"FARMING_FOR_DUMMIES"}, "1") },
            {("farming_for_dummies", "1"), (new(){"FARMING_FOR_DUMMIES"}, string.Empty) },
        };

    public bool TryGetIngredients(string property, string value, string baseValue, out List<string> ingredients)
    {
        if (!propertyToItem.TryGetValue((property, value), out var result))
        {
            ingredients = null;
            return false;
        }
        ingredients = new(result.needed);
        if (baseValue != string.Empty && result.previousLevel != baseValue && TryGetIngredients(property, result.previousLevel, baseValue, out var previousLevelIngredients))
            ingredients.AddRange(previousLevelIngredients);
        return true;
    }

    public Dictionary<ItemReferences.Reforge, (string, int)> ReforgeCosts { get; set; } = new(){
            { ItemReferences.Reforge.ambered, ("AMBER_MATERIAL",300000)},
            { ItemReferences.Reforge.blessed, ("BLESSED_FRUIT",10000)},
            { ItemReferences.Reforge.bulky, ("BULKY_STONE",300000)},
            { ItemReferences.Reforge.waxed, ("BLAZE_WAX",150000)},
            { ItemReferences.Reforge.candied, ("CANDY_CORN",300000)},
            { ItemReferences.Reforge.submerged, ("DEEP_SEA_ORB",750000)},
            { ItemReferences.Reforge.fleet, ("DIAMONITE",250000)},
            { ItemReferences.Reforge.dirty, ("DIRT_BOTTLE",50000)},
            { ItemReferences.Reforge.Fabled, ("DRAGON_CLAW",1000000)},
            { ItemReferences.Reforge.Renowned, ("DRAGON_HORN",1000000)},
            { ItemReferences.Reforge.Spiked, ("DRAGON_SCALE",600000)},
            { ItemReferences.Reforge.Perfect, ("DIAMOND_ATOM",600000)},
            //{ ItemReferences.Reforge.hyper, ("ENDSTONE_GEODE",100000)},
            { ItemReferences.Reforge.coldfusion, ("ENTROPY_SUPPRESSOR",1000000)},
            { ItemReferences.Reforge.Giant, ("GIANT_TOOTH",1000000)},
            { ItemReferences.Reforge.bountiful, ("GOLDEN_BALL",300000)},
            { ItemReferences.Reforge.stiff, ("HARDENED_WOOD",75000)},
            { ItemReferences.Reforge.heated, ("HOT_STUFF",300000)},
            //{ ItemReferences.Reforge.Jerry, ("JERRY_STONE",0)},
            { ItemReferences.Reforge.jaded, ("JADERALD",300000)},
            //{ ItemReferences.Reforge.Chomp, ("KUUDRA_MANDIBLE",300000)},
            { ItemReferences.Reforge.Magnetic, ("LAPIS_CRYSTAL",5000)},
            { ItemReferences.Reforge.Silky, ("LUXURIOUS_SPOOL",5000)},
            { ItemReferences.Reforge.lucky, ("LUCKY_DICE",300000)},
            { ItemReferences.Reforge.Gilded, ("MIDAS_JEWEL",5000000)},
            { ItemReferences.Reforge.fortified, ("METEOR_SHARD",150000)},
            { ItemReferences.Reforge.moil, ("MOIL_LOG",50000)},
            { ItemReferences.Reforge.cubic, ("MOLTEN_CUBE",75000)},
            { ItemReferences.Reforge.Necrotic, ("NECROMANCER_BROOCH",300000)},
            { ItemReferences.Reforge.fruitful, ("ONYX",2500)},
            { ItemReferences.Reforge.Precise, ("OPTICAL_LENS",600000)},
            { ItemReferences.Reforge.stellar, ("PETRIFIED_STARFALL",400000)},
            { ItemReferences.Reforge.ancient, ("PRECURSOR_GEAR",50000)},
            { ItemReferences.Reforge.undead, ("PREMIUM_FLESH",150000)},
            { ItemReferences.Reforge.mithraic, ("PURE_MITHRIL",250000)},
            { ItemReferences.Reforge.pitchin, ("PITCHIN_KOI",120000)},
            { ItemReferences.Reforge.Reinforced, ("RARE_DIAMOND",50000)},
            { ItemReferences.Reforge.ridiculous, ("RED_NOSE",150000)},
            { ItemReferences.Reforge.Loving, ("RED_SCARF",600000)},
            { ItemReferences.Reforge.Refined, ("REFINED_AMBER",10000)},
            { ItemReferences.Reforge.Auspicious, ("ROCK_GEMSTONE",300000)},
            { ItemReferences.Reforge.empowered, ("SADAN_BROOCH",1000000)},
            { ItemReferences.Reforge.Spiritual, ("SPIRIT_DECOY",1000000)},
            { ItemReferences.Reforge.suspicious, ("SUSPICIOUS_VIAL",1000000)},
            { ItemReferences.Reforge.strengthened, ("SEARING_STONE",150000)},
            { ItemReferences.Reforge.glistening, ("SHINY_PRISM",150000)}, // wrong in neu
            { ItemReferences.Reforge.toil, ("TOIL_LOG",10000)},
            { ItemReferences.Reforge.aote_stone, ("AOTE_STONE",5000000)},
            { ItemReferences.Reforge.withered, ("WITHER_BLOOD",50000)},
            { ItemReferences.Reforge.headstrong, ("SALMON_OPAL",250000)},
            { ItemReferences.Reforge.mossy, ("OVERGROWN_GRASS",300000)},
            { ItemReferences.Reforge.rooted, ("BURROWING_SPORES",300000)},
        };

    public (string, int) GetReforgeCost(ItemReferences.Reforge reforge, Tier tier = Tier.LEGENDARY)
    {
        var cost = ReforgeCosts.GetValueOrDefault(reforge);
        if (cost == default)
            return (string.Empty, 0);
        if (tier > Tier.LEGENDARY)
            return (cost.Item1, cost.Item2 * 2);
        if (tier < Tier.LEGENDARY)
            return (cost.Item1, cost.Item2 / 2);
        return (cost.Item1, cost.Item2);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="gem">The gem to investigate</param>
    /// <param name="flat">auction flat nbt data to search in</param>
    /// <returns></returns>
    public string GetCorrectGemType(KeyValuePair<string, string> gem, Dictionary<string, string> flat)
    {
        var type = gem.Key.Split("_")[0];
        if (type == "UNIVERSAL" || type == "COMBAT" || type == "DEFENSIVE" || type == "MINING" || type == "OFFENSIVE")
            type = flat.Where(f => f.Key == gem.Key + "_gem").FirstOrDefault().Value;
        return type;
    }

    public string GetItemKeyForGem(KeyValuePair<string, string> gem, Dictionary<string, string> flat)
    {
        var type = GetCorrectGemType(gem, flat);
        return $"{gem.Value}_{type}_GEM";
    }
    public enum Behaviour
    {
        Linear,
        Exp,
        Flag,
        Item // single item required
    }
    public class AttributeDef
    {
        public Behaviour Behaviour;
        public long Max;

        public AttributeDef(Behaviour behaviour, long max)
        {
            Behaviour = behaviour;
            Max = max;
        }
    }
    private Dictionary<(string item, string attr), AttributeDef> AttribDefinitions = new()
    {
        {(String.Empty, "exp"), new AttributeDef(Behaviour.Exp, 25353230)}
    };

    public bool TryGetDefinition(string item, string attr, out AttributeDef def)
    {
        if (AttribDefinitions.TryGetValue((item, attr), out def))
            return true;
        return AttribDefinitions.TryGetValue((String.Empty, attr), out def);
    }

    public long EnchantValue(Enchantment enchant, Dictionary<string, string> flatNbt, Dictionary<string, double> bazaarPrices)
    {
        var key = $"ENCHANTMENT_{enchant.Type.ToString().ToUpper()}_{enchant.Level}";
        if (enchant.Type == Enchantment.EnchantmentType.ultimate_duplex)
            key = $"ENCHANTMENT_{Enchantment.EnchantmentType.ultimate_reiterate}_{enchant.Level}".ToUpper();

        if (bazaarPrices.TryGetValue(key, out var matchingPrice) && matchingPrice > 0 && matchingPrice < 500_000_000)
            return (long)matchingPrice;
        else if (enchant.Type == Enchantment.EnchantmentType.efficiency && enchant.Level >= 6)
        {
            var singleLevelPrice = bazaarPrices.GetValueOrDefault("SIL_EX", 8_000_000);
            return (long)(singleLevelPrice * (enchant.Level - 5));
        }
        else
        {
            // from lvl 1 ench
            key = $"ENCHANTMENT_{enchant.Type.ToString().ToUpper()}_1";
            if (bazaarPrices.ContainsKey(key) && bazaarPrices[key] > 0)
                if (Constants.EnchantToAttribute.TryGetValue(enchant.Type, out (string attrName, double factor, int max) attrData))
                {
                    if (flatNbt == null)
                        return (long)(bazaarPrices[key] + attrData.factor * attrData.max * enchant.Level / 10);
                    var stringValue = flatNbt.GetValueOrDefault(attrData.attrName) ?? "0";
                    return (long)(bazaarPrices[key] + Math.Min(float.Parse(stringValue), attrData.max) * attrData.factor);
                }
                else
                    return (long)(bazaarPrices[key] * Math.Pow(2, enchant.Level - 1));
        }
        return -1;
    }
}