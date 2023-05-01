using System;
using System.Collections.Generic;
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
            ItemReferences.Reforge.ancient,
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
            new Enchantment(Enchantment.EnchantmentType.true_protection,1),
            new Enchantment(Enchantment.EnchantmentType.smite,7),
            new Enchantment(Enchantment.EnchantmentType.critical,8),
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
            new Enchantment(Enchantment.EnchantmentType.ultimate_jerry,6),
            new Enchantment(Enchantment.EnchantmentType.ultimate_last_stand,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_no_pain_no_gain,5),
            new Enchantment(Enchantment.EnchantmentType.ultimate_rend,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_swarm,3),
            new Enchantment(Enchantment.EnchantmentType.ultimate_wise,3),
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
            {Enchantment.EnchantmentType.PROSECUTE, 6},
            {Enchantment.EnchantmentType.growth, 7},
            {Enchantment.EnchantmentType.ultimate_chimera, 1},
            {Enchantment.EnchantmentType.smoldering, 5},
            {Enchantment.EnchantmentType.critical, 7},
            {Enchantment.EnchantmentType.ultimate_reiterate, 2},
            {Enchantment.EnchantmentType.venomous, 6},
            {Enchantment.EnchantmentType.ferocious_mana, 6},
            {Enchantment.EnchantmentType.power, 7},
            {Enchantment.EnchantmentType.ultimate_bobbin_time, 5},
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
            {Enchantment.EnchantmentType.efficiency, 8},
            {Enchantment.EnchantmentType.divine_gift, 1}
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


        public static Enchantment SelectBest(IEnumerable<Enchantment> enchants)
        {
            if(enchants == null)
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