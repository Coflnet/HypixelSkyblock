using System;
using System.Collections.Generic;
using System.Linq;

namespace Coflnet.Sky.Core;
/// <summary>
/// Maps properties to items to be able to get their cost
/// </summary>
public class PropertyMapper
{
    private const string UseCount = "use_count_the-count";
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
            { ("divan_powder_coating", "1"), (["DIVAN_POWDER_COATING"], string.Empty)},
            { ("wood_singularity_count", "1"), (new(){"WOOD_SINGULARITY"}, string.Empty)},
            { ("jalapeno_count", "1"), (new(){"JALAPENO_BOOK"}, string.Empty) },
            { ("stats_book", "*"), (new(){"BOOK_OF_STATS"}, string.Empty) },
            { ("tuned_transmission", "*"), (new(){"TRANSMISSION_TUNER"}, UseCount) },
            { ("mana_disintegrator_count", "*"), (new(){"MANA_DISINTEGRATOR"}, UseCount) },
            { ("polarvoid", "*"), (new(){"POLARVOID_BOOK"}, UseCount) },
            { ("bookworm_books", "*"), (new(){"BOOKWORM_BOOK"}, UseCount) },
            {("hpc", "15"), (new(){"FUMING_POTATO_BOOK"}, "14") },
            {("hpc", "14"), (new(){"FUMING_POTATO_BOOK"}, "13") },
            {("hpc", "13"), (new(){"FUMING_POTATO_BOOK"}, "12") },
            {("hpc", "12"), (new(){"FUMING_POTATO_BOOK"}, "11") },
            {("hpc", "11"), (new(){"FUMING_POTATO_BOOK"}, "10") },
            {("hpc", "*"), (new(){"HOT_POTATO_BOOK"}, UseCount)},
            {("hotpc", "1"), (new(){"FUMING_POTATO_BOOK","FUMING_POTATO_BOOK","FUMING_POTATO_BOOK","FUMING_POTATO_BOOK"}, "0") },
            // 0 is > 10 (including one or more fummings)
            {("hotpc", "0"), (new(){"FUMING_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK","HOT_POTATO_BOOK"}, String.Empty)},
            {("farming_for_dummies_count", "*"), (new(){"FARMING_FOR_DUMMIES"}, UseCount)}
        };
    private HashSet<string> ContainsItemId = new HashSet<string>(NBT.KeysWithItem)
    {
    };

    public bool TryGetIngredients(string property, string value, string baseValue, out List<string> ingredients)
    {
        if (ContainsItemId.Contains(property) && value != string.Empty)
        {
            ingredients = new() { value.Trim('"') };
            return true;
        }
        if (property.StartsWith("RUNE_"))
        {
            // either exact level match for music rune from sniper, general match for the rune or unique rune prefix ommited when parsing
            ingredients = new() { $"{property}_{value}" };
            var level = int.Parse(value);
            var requiredCount = (int)Math.Pow(3.5, level - 1);
            if (new string[] { "RUNE_GOLDEN_CARPET", "RUNE_BARK_TUNES", "RUNE_MEOW_MUSIC", "RUNE_PRIMAL_FEAR", "RUNE_GRAND_FREEZING", "RUNE_SPELLBOUND", "RUNE_GRAND_SEARING" }.Contains(property))
                requiredCount = 1;
            ingredients.AddRange(Enumerable.Repeat(property, requiredCount));
            ingredients.AddRange(Enumerable.Repeat("UNIQUE_" + property, 1)); // unique runes from firesales always have level 3
            return true;
        }
        if (property == "ability_scroll")
        {
            ingredients = value.Split(' ', ',').Except(baseValue?.Split(' ') ?? new string[0]).ToList();
            return true;
        }
        if (property == "talisman_enrichment")
        {
            if (value == "yes") // set by sniper to group "any", returning highest volume enrichment
                ingredients = new() { "TALISMAN_ENRICHMENT_FEROCITY" };
            else if (string.IsNullOrEmpty(baseValue))
                ingredients = new() { "TALISMAN_ENRICHMENT_" + value.ToUpper() };
            else
                // was swapped
                ingredients = new() { "TALISMAN_ENRICHMENT_SWAPPER" };
            return true;
        }
        if (propertyToItem.TryGetValue((property, value), out var result))
        {
            ingredients = MapIngredients(property, value, baseValue, result);
            return true;
        }

        if (propertyToItem.TryGetValue((property, "*"), out var singleItem) && string.IsNullOrEmpty(baseValue))
        {
            ingredients = new(singleItem.needed);
            if (int.TryParse(value, out var count) && count > 1 && singleItem.previousLevel == UseCount)
            {
                ingredients = new(Enumerable.Repeat(singleItem.needed, count).SelectMany(x => x));
                return true;
            }
            return true;
        }

        if (singleItem.previousLevel == UseCount)
        {
            int.TryParse(baseValue, out var baseCount);
            if (int.TryParse(value, out var count) && count - baseCount > 1)
            {
                ingredients = new(Enumerable.Repeat(singleItem.needed, count - baseCount).SelectMany(x => x));
                return true;
            }
        }
        if (!propertyToItem.TryGetValue((property, value), out result))
        {
            ingredients = null;
            return false;
        }
        ingredients = MapIngredients(property, value, baseValue, result);
        return true;

        List<string> MapIngredients(string property, string value, string baseValue, (List<string> needed, string previousLevel) result)
        {
            List<string> ingredients = new(result.needed);
            if (baseValue != string.Empty
                            && result.previousLevel != baseValue
                            && result.previousLevel != value // safeguard break recurision
                            && TryGetIngredients(property, result.previousLevel, baseValue, out var previousLevelIngredients))
                ingredients.AddRange(previousLevelIngredients);
            return ingredients;
        }
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
            { ItemReferences.Reforge.festive, ("FROZEN_BAUBLE", 400000)},
            { ItemReferences.Reforge.snowy, ("TERRY_SNOWGLOBE", 200000)},
            { ItemReferences.Reforge.chomp, ("KUUDRA_MANDIBLE", 300000)},
            { ItemReferences.Reforge.fanged, ("FULL_JAW_FANGING_KIT", 100000)},
            { ItemReferences.Reforge.blood_soaked, ("PRESUMED_GALLON_OF_RED_PAINT", 150000)},
            { ItemReferences.Reforge.Fang_tastic_chocolate_chip, ("CHOCOLATE_CHIP", 0)},
            { ItemReferences.Reforge.bubba_blister, ("BUBBA_BLISTER", 0)},
        };

    public (string, int) GetReforgeCost(ItemReferences.Reforge reforge, Tier tier = Tier.LEGENDARY)
    {
        if (NeuReforgeLookup.TryGetValue(reforge, out var neucost))
            return (neucost.itemTag, neucost.Item1.GetValueOrDefault(tier));
        var cost = ReforgeCosts.GetValueOrDefault(reforge);
        if (cost == default)
            return (string.Empty, 0);
        if (tier > Tier.LEGENDARY)
            return (cost.Item1, cost.Item2 * 2);
        if (tier < Tier.LEGENDARY)
            return (cost.Item1, cost.Item2 / 2);
        return (cost.Item1, cost.Item2);
    }

    public Dictionary<ItemReferences.Reforge, (Dictionary<Tier, int>, string itemTag)> NeuReforgeLookup = new();

    public async System.Threading.Tasks.Task LoadNeuConstants()
    {
        var json = await System.IO.File.ReadAllTextAsync("NEU-REPO/constants/reforgestones.json");
        var reforgeCosts = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, ReforgeElement>>(json);
        foreach (var reforge in reforgeCosts)
        {
            var costs = new Dictionary<Tier, int>();
            foreach (var cost in reforge.Value.reforgeCosts)
            {
                var tier = Enum.Parse<Tier>(cost.Key.Replace(" ", "_"), true);
                costs.Add(tier, cost.Value);
            }
            if (!Enum.TryParse<ItemReferences.Reforge>(reforge.Value.internalName.Replace("-", "_"), true, out var reforgeEnum))
                if (!Enum.TryParse<ItemReferences.Reforge>(reforge.Value.reforgeName.Replace("-", "_").Replace("'", ""), true, out reforgeEnum))
                {
                    Console.WriteLine($"Could not parse reforge {reforge.Value.internalName} or {reforge.Value.reforgeName}");
                    continue;
                }
            NeuReforgeLookup.Add(reforgeEnum, (costs, reforge.Key));
        }

    }

    public class ReforgeElement
    {
        public string reforgeName;
        public string internalName;
        public Dictionary<string, int> reforgeCosts;
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
        if (type == "UNIVERSAL" || type == "COMBAT" || type == "DEFENSIVE"
            || type == "MINING" || type == "OFFENSIVE" || type == "CHISEL")
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
        var lvl1Key = $"ENCHANTMENT_{enchant.Type.ToString().ToUpper()}_1";
        if (enchant.Type == Enchantment.EnchantmentType.ultimate_duplex)
        {
            key = $"ENCHANTMENT_ULTIMATE_REITERATE_{enchant.Level}".ToUpper();
            lvl1Key = $"ENCHANTMENT_ULTIMATE_REITERATE_1";
        }
        if (bazaarPrices.TryGetValue(key, out var matchingPrice) && matchingPrice > 0 && matchingPrice < 500_000_000
            && (!bazaarPrices.TryGetValue(lvl1Key, out var lvl1Price) || lvl1Price <= matchingPrice)
        )
            return (long)matchingPrice;
        else if (enchant.Type == Enchantment.EnchantmentType.efficiency && enchant.Level >= 6)
        {
            var singleLevelPrice = bazaarPrices.GetValueOrDefault("SIL_EX", 8_000_000);
            // adding to stonk would only need 4 levels
            return (long)(singleLevelPrice * (enchant.Level - 5));
        }
        else if (enchant.Type == Enchantment.EnchantmentType.scavenger && enchant.Level == 6)
        {
            return (long)bazaarPrices.GetValueOrDefault("GOLDEN_BOUNTY", 1_000_000);
        }
        else
        {
            // from lvl 1 ench
            if (bazaarPrices.ContainsKey(lvl1Key) && bazaarPrices[lvl1Key] > 0)
                if (Constants.EnchantToAttribute.TryGetValue(enchant.Type, out (string attrName, double factor, int max) attrData))
                {
                    if (flatNbt == null)
                        return (long)(bazaarPrices[lvl1Key] + attrData.factor * attrData.max * enchant.Level / 10);
                    var stringValue = flatNbt.GetValueOrDefault(attrData.attrName) ?? "0";
                    return (long)(bazaarPrices[lvl1Key] + Math.Min(float.Parse(stringValue), attrData.max) * attrData.factor);
                }
                else
                    return (long)(bazaarPrices[lvl1Key] * Math.Pow(2, enchant.Level - 1));
        }
        return -1;
    }

    public long CraftCostSum(Dictionary<string, string> attributes, List<string>? formatted)
    {
        long sum = 0;



        return sum;
    }
}
