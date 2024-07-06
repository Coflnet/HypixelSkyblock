using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Core
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
            //ItemReferences.Reforge.ancient, fell to just 600k and low volume
            //ItemReferences.Reforge.Necrotic, avg bazaar price 40k, max apply cost 600k usually 300k or lower
            ItemReferences.Reforge.Gilded,
            ItemReferences.Reforge.withered,
            ItemReferences.Reforge.Spiritual,
            ItemReferences.Reforge.jaded, // (sorrow armor and divan, maybe just above some tier)
            ItemReferences.Reforge.warped, // for aote
            ItemReferences.Reforge.aote_stone, // warped on aote :shrug:
            ItemReferences.Reforge.toil,
            // only 50k ItemReferences.Reforge.moil, // on axe
            ItemReferences.Reforge.Fabled,
            ItemReferences.Reforge.Giant,
            ItemReferences.Reforge.submerged, // for shark armor
            ItemReferences.Reforge.Renowned, // for superior, sorrow armor
            ItemReferences.Reforge.mossy,
            ItemReferences.Reforge.rooted,
            ItemReferences.Reforge.festive,
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
            new Enchantment(Enchantment.EnchantmentType.dragon_hunter,2),
            new Enchantment(Enchantment.EnchantmentType.snipe,4),
            new Enchantment(Enchantment.EnchantmentType.pristine,2), // maybe 1 as well
            new Enchantment(Enchantment.EnchantmentType.overload,2),
            //new Enchantment(Enchantment.EnchantmentType.true_protection,1), cost alsmot 1m but is mostly useless
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
            new Enchantment(Enchantment.EnchantmentType.ultimate_jerry,6),
            new Enchantment(Enchantment.EnchantmentType.ultimate_last_stand,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_no_pain_no_gain,5),
            new Enchantment(Enchantment.EnchantmentType.ultimate_rend,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_swarm,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_wise,4),
            new Enchantment(Enchantment.EnchantmentType.ultimate_wisdom,3),
            new Enchantment(Enchantment.EnchantmentType.compact,9),
            new Enchantment(Enchantment.EnchantmentType.counter_strike,5),
            new Enchantment(Enchantment.EnchantmentType.smarty_pants,2),
            new Enchantment(Enchantment.EnchantmentType.cultivating,1),
            new Enchantment(Enchantment.EnchantmentType.smoldering,1),
            new Enchantment(Enchantment.EnchantmentType.strong_mana,5),
            new Enchantment(Enchantment.EnchantmentType.hardened_mana,5),
            new Enchantment(Enchantment.EnchantmentType.mana_vampire,4),
            new Enchantment(Enchantment.EnchantmentType.ferocious_mana,2),
            new Enchantment(Enchantment.EnchantmentType.charm,4),
            new Enchantment(Enchantment.EnchantmentType.big_brain,3),
            new Enchantment(Enchantment.EnchantmentType.cayenne,5),
            new Enchantment(Enchantment.EnchantmentType.divine_gift,1),
            new Enchantment(Enchantment.EnchantmentType.green_thumb,1),
            new Enchantment(Enchantment.EnchantmentType.prosperity,1),
            new Enchantment(Enchantment.EnchantmentType.dedication,1),
            new Enchantment(Enchantment.EnchantmentType.tabasco,3),
            new Enchantment(Enchantment.EnchantmentType.fire_aspect,3),
            new Enchantment(Enchantment.EnchantmentType.power,7),
            new Enchantment(Enchantment.EnchantmentType.pesterminator,1),
            new Enchantment(Enchantment.EnchantmentType.ultimate_refrigerate,1),
            // new Enchantment(Enchantment.EnchantmentType.quantum,5), https://discord.com/channels/267680588666896385/1184721061900734604/1187048778339983440
            new Enchantment(Enchantment.EnchantmentType.ultimate_the_one,4),
            new Enchantment(Enchantment.EnchantmentType.reflection,5),
            new Enchantment(Enchantment.EnchantmentType.expertise,10),
            new (Enchantment.EnchantmentType.paleontologist, 1),
            new (Enchantment.EnchantmentType.ice_cold, 1),
            new (Enchantment.EnchantmentType.toxophilite, 1),
        };

        /// <summary>
        /// Enchants at or above 20m
        /// </summary>
        public readonly static Dictionary<Enchantment.EnchantmentType, byte> VeryValuableEnchant = new()
        {
            {Enchantment.EnchantmentType.execute, 6},
            {Enchantment.EnchantmentType.ultimate_legion, 5},
            {Enchantment.EnchantmentType.big_brain, 4},
            {Enchantment.EnchantmentType.cayenne, 5},
            {Enchantment.EnchantmentType.chance, 5},
            {Enchantment.EnchantmentType.counter_strike, 5},
            {Enchantment.EnchantmentType.turbo_cactus, 5},
            {Enchantment.EnchantmentType.ender_slayer, 7},
            {Enchantment.EnchantmentType.sharpness, 7},
            {Enchantment.EnchantmentType.strong_mana, 8},
            {Enchantment.EnchantmentType.hardened_mana, 9},
            {Enchantment.EnchantmentType.mana_vampire, 9},
            {Enchantment.EnchantmentType.prosecute, 6},
            {Enchantment.EnchantmentType.growth, 7},
            {Enchantment.EnchantmentType.ultimate_chimera, 1},
            {Enchantment.EnchantmentType.smoldering, 5},
            {Enchantment.EnchantmentType.critical, 7},
            {Enchantment.EnchantmentType.ultimate_reiterate, 2},
            {Enchantment.EnchantmentType.venomous, 6},
            {Enchantment.EnchantmentType.ferocious_mana, 6},
            {Enchantment.EnchantmentType.power, 7},
            {Enchantment.EnchantmentType.ultimate_bobbin_time, 3},
            {Enchantment.EnchantmentType.first_strike, 5},
            {Enchantment.EnchantmentType.ultimate_flash, 5},
            {Enchantment.EnchantmentType.ultimate_fatal_tempo, 1},
            {Enchantment.EnchantmentType.snipe, 4},
            {Enchantment.EnchantmentType.vicious, 5},
            {Enchantment.EnchantmentType.giant_killer, 7},
            {Enchantment.EnchantmentType.triple_strike, 5},
            {Enchantment.EnchantmentType.ultimate_inferno, 1},
            {Enchantment.EnchantmentType.syphon, 5},
            {Enchantment.EnchantmentType.looting, 5},
            {Enchantment.EnchantmentType.cubism, 6},
            {Enchantment.EnchantmentType.luck, 7},
            {Enchantment.EnchantmentType.titan_killer, 7},
            {Enchantment.EnchantmentType.efficiency, 8},
            {Enchantment.EnchantmentType.divine_gift, 1},
            {Enchantment.EnchantmentType.green_thumb, 3},
            {Enchantment.EnchantmentType.prosperity, 4},
            {Enchantment.EnchantmentType.dedication, 4},
            {Enchantment.EnchantmentType.pesterminator,4 },
            {Enchantment.EnchantmentType.ultimate_the_one,4}
        };

        private static List<int> WorthOrder = new List<int>()
        {
            70,78,75,77,97,20,34,95,17,103,3,5,4,37,90,73,50,9,8,96,1,99,15,69,76,18,91,46,2,102,
            16,23,93,7,94,6,10,72,12,14,74,13,11,60,38,89,35,57,33,24,71,58,43,40,100,45,65,67,41,
            30,55,31,68,22,85,28,21,29,42,53,83,92,84,56,39,86,80,32,87,81,49,27,48,101,82,79,
            47,19,61,26,51,44,64,63,88,62,52,25,54,36,66
        };


        /*
        SELECT Type,Level FROM `Enchantment`,Auctions as a
        where a.Id = SaveAuctionId
        and SaveAuctionId > 35092256
        and a.ItemId = 1339
        and HighestBidAmount > 0
        group by Type,Level
        order by avg(HighestBidAmount) desc
        */
        private static List<(int, int)> WorthOrderLevels = new()
        {
            (70,2),(18,7),(91,5),(35,5),(23,4),(7,5),(95,5),(70,1),(75,5),(50,7),(12,7),(3,6),(2,7),
            (78,5),(8,7),(4,7),(5,6),(37,7),(46,7),(17,6),(77,5),(11,5),(89,5),(13,5),(20,5),(77,4),
            (75,3),(73,5),(96,5),(1,6),(103,5),(74,5),(97,1),(72,5),(37,1),(90,6),(102,5),(73,4),
            (77,3),(96,4),(94,7),(103,4),(20,4),(43,7),(71,5),(93,6),(40,7),(72,4),(18,6),(102,4),
            (73,3),(96,3),(46,6),(74,4),(103,3),(20,3),(65,5),(67,5),(24,1),(72,3),(23,1),(34,2),
            (68,5),(4,6),(85,5),(84,5),(102,3),(80,5),(71,4),(33,7),(73,2),(96,2),(83,5),(45,7),
            (74,3),(93,4),(103,2),(65,4),(33,1),(20,2),(6,4),(67,4),(38,7),(86,5),(71,3),(9,3),(16,6),
            (37,6),(34,1),(84,4),(18,1),(99,1),(69,1),(68,4),(24,6),(76,1),(72,2),(85,4),(21,8),(79,5),
            (80,4),(3,5),(8,6),(81,5),(15,1),(10,6),(17,5),(33,5),(5,5),(92,5),(1,5),(102,2),(83,4),(45,1),
            (73,1),(96,1),(14,6),(58,3),(21,9),(64,5),(65,3),(103,1),(50,4),(91,4),(84,3),(60,1),(63,5),
            (50,6),(7,4),(74,2),(86,4),(31,4),(67,3),(94,6),(20,1),(57,1),(82,5),(2,3),(32,3),(30,3),
            (19,3),(88,5),(81,4),(42,8),(10,5),(42,9),(79,4),(68,3),(61,5),(85,3),(71,2),(11,4),(11,3),
            (13,3),(16,5),(80,3),(13,4),(72,1),(6,3),(18,3),(100,5),(8,5),(35,3),(21,7),(12,6),(102,1),
            (12,5),(2,5),(32,4),(52,1),(2,6),(63,4),(92,4),(58,2),(83,3),(87,5),(64,4),(56,4),(35,4),(82,4),
            (38,1),(4,5),(4,3),(74,1),(90,5),(86,3),(79,3),(66,5),(53,4),(71,1),(88,4),(50,3),(81,3),(37,5),
            (61,4),(41,3),(65,2),(67,2),(23,3),(89,4),(42,1),(84,2),(93,5),(58,1),(82,3),(42,10),(29,6),
            (55,6),(30,6),(40,6),(68,2),(14,5),(28,5),(22,1),(33,4),(95,4),(87,4),(31,6),(38,5),(85,2),
            (80,2),(41,2),(17,3),(18,4),(38,6),(63,3),(55,4),(51,2),(56,6),(3,2),(50,5),(21,10),(66,4),
            (87,1),(37,4),(32,5),(101,2),(65,1),(27,6),(39,1),(44,2),(88,3),(32,6),(83,2),(49,1),(87,3),
            (30,5),(86,2),(91,3),(48,3),(19,5),(92,3),(29,4),(67,1),(24,5),(61,3),(62,5),(101,1),(101,3),
            (81,2),(42,6),(68,1),(64,3),(21,2),(46,3),(79,2),(28,4),(89,3),(43,6),(47,3),(29,5),(85,1),
            (36,2),(82,2),(37,2),(17,4),(42,7),(1,4),(27,5),(21,5),(84,1),(26,1),(24,4),(61,2),(4,4),(46,5),
            (87,2),(21,6),(83,1),(45,6),(80,1),(79,1),(92,2),(88,2),(86,1),(62,1),(63,2),(81,1),(13,2),(66,3),
            (89,2),(7,3),(62,4),(18,5),(30,4),(56,5),(4,2),(42,5),(10,4),(62,3),(46,4),(52,5),(51,1),(12,4),
            (54,1),(64,2),(35,2),(25,1),(92,1),(88,1),(82,1),(46,1),(63,1),(36,1),(53,3),(44,1),(3,4),(61,1),
            (16,4),(8,4),(45,5),(55,5),(31,5),(90,4),(9,2),(5,4),(91,2),(2,4),(33,6),(64,1),(62,2),(19,4),
            (27,4),(95,3),(47,2),(11,2),(6,2),(94,4),(50,1),(40,5),(45,3),(94,5),(48,2),(43,4),(43,5),(66,2),
            (31,1),(14,4),(37,3),(52,4),(46,2),(23,2),(53,2),(21,3),(40,4),(52,3),(66,1),(13,1),(52,2),(42,4),
            (38,4),(24,2),(17,2),(21,4),(43,1),(45,4),(21,1),(45,2),(24,3),(38,3),(40,3),(38,2),(9,1)
        };

        /// <summary>
        /// Keys of attributes - only two out of these exist on any given item at the same time
        /// </summary>
        public static readonly ImmutableHashSet<string> AttributeKeys = new HashSet<string>(){
                "lifeline", "breeze", "speed", "experience", "mana_pool",
                "life_regeneration", "blazing_resistance", "arachno_resistance",
                "undead_resistance",
                "blazing_fortune", "fishing_experience", "double_hook", "infection",
                "trophy_hunter", "fisherman", "hunter", "fishing_speed",
                "life_recovery", "ignition", "combo", "attack_speed", "midas_touch",
                "mana_regeneration", "veteran", "mending", "ender_resistance", "dominance", "ender", "mana_steal", "blazing",
                "elite", "arachno", "undead",
                "warrior", "deadeye", "fortitude", "magic_find"
                }.ToImmutableHashSet();

        public static readonly ImmutableDictionary<Enchantment.EnchantmentType, (string, double, int)> EnchantToAttribute = new Dictionary<Enchantment.EnchantmentType, (string, double, int)>(){
            { Enchantment.EnchantmentType.cultivating, ("farmed_cultivating",0.1, 100_000_000)},
            { Enchantment.EnchantmentType.champion, ("champion_combat_xp",1,3_000_000)},
            { Enchantment.EnchantmentType.toxophilite, ("toxophilite_combat_xp",1,3_000_000)},
            { Enchantment.EnchantmentType.compact, ("compact_blocks", 6, 1_000_000)},
            { Enchantment.EnchantmentType.hecatomb, ("hecatomb_s_runs", 200_000, 100)},
            { Enchantment.EnchantmentType.expertise, ("expertise_kills", 1000, 15_000)}
        }.ToImmutableDictionary();


        /// <summary>
        /// compare to https://github.com/NotEnoughUpdates/NotEnoughUpdates-REPO/blob/master/constants/enchants.json#L1473
        /// </summary>
        public static readonly ImmutableDictionary<Enchantment.EnchantmentType, int> MaxEnchantmentTableLevel = new Dictionary<Enchantment.EnchantmentType, int>()
        {
            { Enchantment.EnchantmentType.sharpness, 5 },
            { Enchantment.EnchantmentType.smite, 5 },
            { Enchantment.EnchantmentType.bane_of_arthropods, 5 },
            { Enchantment.EnchantmentType.looting, 3 },
            { Enchantment.EnchantmentType.cubism, 5 },
            { Enchantment.EnchantmentType.cleave, 5 },
            { Enchantment.EnchantmentType.life_steal, 3 },
            { Enchantment.EnchantmentType.giant_killer, 5 },
            { Enchantment.EnchantmentType.critical, 5 },
            { Enchantment.EnchantmentType.first_strike, 4 },
            { Enchantment.EnchantmentType.triple_strike, 4 },
            { Enchantment.EnchantmentType.ender_slayer, 5 },
            { Enchantment.EnchantmentType.execute, 5 },
            { Enchantment.EnchantmentType.thunderlord, 5 },
            { Enchantment.EnchantmentType.lethality, 5 },
            { Enchantment.EnchantmentType.syphon, 3 },
            { Enchantment.EnchantmentType.vampirism, 5 },
            { Enchantment.EnchantmentType.venomous, 5 },
            { Enchantment.EnchantmentType.thunderbolt, 5 },
            { Enchantment.EnchantmentType.prosecute, 5 },
            { Enchantment.EnchantmentType.titan_killer, 5 },
            { Enchantment.EnchantmentType.luck, 5 },
            { Enchantment.EnchantmentType.protection, 5 },
            { Enchantment.EnchantmentType.blast_protection, 5 },
            { Enchantment.EnchantmentType.projectile_protection, 5 },
            { Enchantment.EnchantmentType.fire_protection, 5 },
            { Enchantment.EnchantmentType.thorns, 3 },
            { Enchantment.EnchantmentType.growth, 5 },
            { Enchantment.EnchantmentType.frost_walker, 2 },
            { Enchantment.EnchantmentType.feather_falling, 5 },
            { Enchantment.EnchantmentType.depth_strider, 3 },
            { Enchantment.EnchantmentType.aqua_affinity, 1 },
            { Enchantment.EnchantmentType.respiration, 3 },
            { Enchantment.EnchantmentType.silk_touch, 1 },
            { Enchantment.EnchantmentType.smelting_touch, 1 },
            { Enchantment.EnchantmentType.fortune, 3 },
            { Enchantment.EnchantmentType.experience, 3 },
            { Enchantment.EnchantmentType.efficiency, 5 },
            { Enchantment.EnchantmentType.harvesting, 5 },
            { Enchantment.EnchantmentType.piscary, 5 }
        }.ToImmutableDictionary();

        public static readonly HashSet<string> Vanilla = ["acacia_door", "acacia_fence", "acacia_fence_gate", "acacia_stairs", "activator_rail", "air", "anvil", "apple", "armor_stand", "arrow", "baked_potato", "banner", "barrier", "bed", "beef", "birch_door", "birch_fence", "birch_fence_gate", "birch_stairs", "blaze_powder", "blaze_rod", "boat", "bone", "book", "bookshelf", "bow", "bowl", "bread", "brewing_stand", "brick", "brick_block", "brick_stairs", "brown_mushroom", "brown_mushroom_block", "bucket", "cactus", "cake", "carpet", "carrot", "carrot_on_a_stick", "cauldron", "chainmail_boots", "chainmail_chestplate", "chainmail_helmet", "chainmail_leggings", "chest", "chest_minecart", "chicken", "clay", "clay_ball", "clock", "coal", "coal_block", "coal_ore", "cobblestone", "cobblestone_wall", "command_block", "command_block_minecart", "comparator", "compass", "cooked_beef", "cooked_chicken", "cooked_fish", "cooked_mutton", "cooked_porkchop", "cooked_rabbit", "cookie", "crafting_table", "dark_oak_door", "dark_oak_fence", "dark_oak_fence_gate", "dark_oak_stairs", "daylight_detector", "deadbush", "detector_rail", "diamond", "diamond_axe", "diamond_block", "diamond_boots", "diamond_chestplate", "diamond_helmet", "diamond_hoe", "diamond_horse_armor", "diamond_leggings", "diamond_ore", "diamond_pickaxe", "diamond_shovel", "diamond_sword", "dirt", "dispenser", "double_plant", "dragon_egg", "dropper", "dye", "egg", "emerald", "emerald_block", "emerald_ore", "enchanted_book", "enchanting_table", "end_portal_frame", "end_stone", "ender_chest", "ender_eye", "ender_pearl", "experience_bottle", "farmland", "feather", "fence", "fence_gate", "fermented_spider_eye", "filled_map", "fire_charge", "firework_charge", "fireworks", "fish", "fishing_rod", "flint", "flint_and_steel", "flower_pot", "furnace", "furnace_minecart", "ghast_tear", "glass", "glass_bottle", "glass_pane", "glowstone", "glowstone_dust", "gold_block", "gold_ingot", "gold_nugget", "gold_ore", "golden_apple", "golden_axe", "golden_boots", "golden_carrot", "golden_chestplate", "golden_helmet", "golden_hoe", "golden_horse_armor", "golden_leggings", "golden_pickaxe", "golden_rail", "golden_shovel", "golden_sword", "grass", "gravel", "gunpowder", "hardened_clay", "hay_block", "heavy_weighted_pressure_plate", "hopper", "hopper_minecart", "ice", "iron_axe", "iron_bars", "iron_block", "iron_boots", "iron_chestplate", "iron_door", "iron_helmet", "iron_hoe", "iron_horse_armor", "iron_ingot", "iron_leggings", "iron_ore", "iron_pickaxe", "iron_shovel", "iron_sword", "iron_trapdoor", "item_frame", "jukebox", "jungle_door", "jungle_fence", "jungle_fence_gate", "jungle_stairs", "ladder", "lapis_block", "lapis_ore", "lava", "lava_bucket", "lead", "leather", "leather_boots", "leather_chestplate", "leather_helmet", "leather_leggings", "leaves", "leaves2", "lever", "light_weighted_pressure_plate", "lit_pumpkin", "log", "log2", "magma_cream", "map", "melon", "melon_block", "melon_seeds", "milk_bucket", "minecart", "mob_spawner", "monster_egg", "mossy_cobblestone", "mushroom_stew", "mutton", "mycelium", "name_tag", "nether_brick", "nether_brick_fence", "nether_brick_stairs", "nether_star", "nether_wart", "netherbrick", "netherrack", "noteblock", "oak_stairs", "obsidian", "packed_ice", "painting", "paper", "piston", "planks", "poisonous_potato", "porkchop", "potato", "potion", "prismarine", "prismarine_crystals", "prismarine_shard", "pumpkin", "pumpkin_pie", "pumpkin_seeds", "quartz", "quartz_block", "quartz_ore", "quartz_stairs", "rabbit", "rabbit_foot", "rabbit_hide", "rabbit_stew", "rail", "record_11", "record_13", "record_blocks", "record_cat", "record_chirp", "record_far", "record_mall", "record_mellohi", "record_stal", "record_strad", "record_wait", "record_ward", "red_flower", "red_mushroom", "red_mushroom_block", "red_sandstone", "red_sandstone_stairs", "redstone", "redstone_block", "redstone_lamp", "redstone_ore", "redstone_torch", "reeds", "repeater", "rotten_flesh", "saddle", "sand", "sandstone", "sandstone_stairs", "sapling", "sea_lantern", "shears", "sign", "skull", "slime", "slime_ball", "snow", "snow_layer", "snowball", "soul_sand", "spawn_egg", "speckled_melon", "spider_eye", "sponge", "spruce_door", "spruce_fence", "spruce_fence_gate", "spruce_stairs", "stained_glass", "stained_glass_pane", "stained_hardened_clay", "stick", "sticky_piston", "stone", "stone_axe", "stone_brick_stairs", "stone_button", "stone_hoe", "stone_pickaxe", "stone_pressure_plate", "stone_shovel", "stone_slab", "stone_slab2", "stone_stairs", "stone_sword", "stonebrick", "string", "sugar", "tallgrass", "tnt", "tnt_minecart", "torch", "trapdoor", "trapped_chest", "tripwire_hook", "vine", "water", "water_bucket", "waterlily", "web", "wheat", "wheat_seeds", "wooden_axe", "wooden_button", "wooden_door", "wooden_hoe", "wooden_pickaxe", "wooden_pressure_plate", "wooden_shovel", "wooden_slab", "wooden_sword", "wool", "writable_book", "written_book", "yellow_flower", 
            // special glitched items 
            "stationary_water", "stationary_lava"];

        public static Enchantment SelectBest(IEnumerable<Enchantment> enchants)
        {
            if (enchants == null)
                return new Enchantment();
            // match exact level
            foreach (var item in WorthOrderLevels)
            {
                foreach (var enchant in enchants)
                {
                    if ((int)enchant.Type == item.Item1 && enchant.Level == item.Item2)
                        return enchant;
                }
            }
            foreach (var item in WorthOrder)
            {
                foreach (var enchant in enchants)
                {
                    if ((int)enchant.Type == item)
                        return enchant;
                }
            }
            return new Enchantment();
        }

        public static double SkyblockYear(DateTime now) => (now - new DateTime(2019, 6, 11, 17, 55, 0, DateTimeKind.Utc)).TotalDays / (TimeSpan.FromDays(5) + TimeSpan.FromHours(4)).TotalDays;

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

        public static bool DoesRecombMatter(Category category, string tag)
        {
            if (category == Category.WEAPON || category == Category.ARMOR || category == Category.ACCESSORIES
                || category == Category.UNKNOWN || tag == null) // the description doesn't know the category
                return true;
            string[] endings = ["CLOAK", "NECKLACE", "BELT", "GLOVES", "BRACELET", "HOE", "PICKAXE", "GAUNTLET", "WAND", "ROD", "DRILL", "INFINI_VACUMM", "POWER_ORB", "GRIFFIN_UPGRADE_STONE_EPIC"];
            return endings.Any(tag.Contains);
        }
    }
}