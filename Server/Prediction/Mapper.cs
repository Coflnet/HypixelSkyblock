using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Core.Prediction
{
    /// <summary>
    /// Mapps db to model format
    /// </summary>
    public class Mapper
    {
        public static Mapper Instance { get; }

        static Mapper()
        {
            Instance = new Mapper();
        }

        private static readonly List<string> RelevantKeys = new List<string>()
        {
            "color",
            "dungeon_item_level",
            "rarity_upgrades",
            "hpc",
            "exp",
            "candyUsed",
            "potion_level",
            "potion_type",
            "splash",
            "level",
            "effect",
            "duration_ticks",
            "blood_god_kills",
            "dungeon_skill_req",
            "baseStatBoostPercentage",
            "item_durability",
            "item_tier",
            "expertise_kills",
            "winning_bid",
            "new_years_cake",
            "zombie_kills",
            "skin",
            "entity_required",
            "compact_blocks",
            "ZOMBIE_SLAYER",
            "talisman_enrichment",
            "bow_kills",
            "spider_kills",
            "raider_kills",
            "backpack_color",
            "wood_singularity_count",
            "ultimateSoulEaterData",
            "sword_kills",
            "enhanced",
            "extended",
            "ranchers_speed",
            "repelling_color",
            "event",
            "drill_part_fuel_tank",
            "ammo",
            "dungeon_paper_id",
            "drill_part_upgrade_module",
            "ability_scroll",
            "art_of_war_count",
            "mob_id",
            "farming_for_dummies_count",
            "bottle_of_jyrre_seconds",
            "leaderVotes",
            "leaderPosition",
            "captured_player",
            "captured_date",
            "initiator_player",
            "party_hat_year",
            "party_hat_color",
            "caffeinated",
            "fishes_caught",
            "caffeinated",
            "fishes_caught",
            "trainingWeightsHeldTime",
            "fungi_cutter_mode",
            "magmaCubesKilled",
            "blocksBroken",
            "radius",
            "maxed_stats",
            "drill_part_engine",
            "dungeon_potion",
            "trapsDefused",
            "skeletorKills",
            "should_give_alchemy_exp",
            "farmed_cultivating",
            "health",
            "mixins",
            "tuning_fork_tuning",
            "zombiesKilled",
            "tear_filled",
            "slayer_energy"
    };

        public ConcurrentDictionary<short, short> KeysToInclude = new ConcurrentDictionary<short, short>();

        public void ExportBatch(int page)
        {
            var path = System.IO.Path.Combine("export",page.ToString());
        }

        public async Task<List<PreditionInput>> GetBatch(int page, int count = 1000)
        {
            using(var context = new HypixelContext())
            {
                return (await context.Auctions
                        .Where(a=>a.End > new DateTime(2021,5,22) && a.HighestBidAmount > 0)
                        .Skip(page * count)
                        .Take(count)
                        .Include(a=>a.NBTLookup)
                        .Include(a=>a.Enchantments)
                        .ToListAsync())
                        .Select(a=>Map(a,a.End))
                        .ToList();
            }
        }

        public PreditionInput Map(SaveAuction auction, DateTime time)
        {
            return new PreditionInput()
            {
                AnvilUses = auction.AnvilUses,
                Bin = auction.Bin,
                Category = auction.Category,
                End = time,
                HighestBid = auction.HighestBidAmount,
                ItemId = auction.ItemId,
                Rarity = (int)auction.Tier,
                Reforge = (int)auction.Reforge,
                Start = auction.Start,
                StartingBid = (int)auction.StartingBid,
                Enchantments = auction.Enchantments.Select(e => ((byte)e.Type, (int)e.Level)).ToList(),
                NbtData = auction.NBTLookup.Select(l =>
                {
                    if (KeysToInclude.TryGetValue(l.KeyId, out short mapped))
                        return (mapped, l.Value);
                    return ((short)0, 0L);
                }).Where(el => el.Item1 != 0).ToList()

            };
        }

    }
}