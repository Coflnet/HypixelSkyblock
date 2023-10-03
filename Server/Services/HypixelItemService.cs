using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Core.Services;

public class HypixelItemService
{

    private readonly HttpClient _httpClient;
    private readonly ILogger<HypixelItemService> _logger;
    private Dictionary<string, Item> _items;

    public HypixelItemService(HttpClient httpClient, ILogger<HypixelItemService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, Item>> GetItemsAsync()
    {
        if (_items != null)
            return _items;

        if (File.Exists("items.json"))
            return JsonSerializer.Deserialize<Root>(await File.ReadAllTextAsync("items.json"))
                .Items.Where(x => x.Id != null).ToDictionary(x => x.Id);
        var response = await _httpClient.GetAsync("https://api.hypixel.net/resources/skyblock/items");
        if (response.IsSuccessStatusCode)
        {
            var responseStream = await response.Content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<Root>(responseStream);
            _items = data.Items.Where(x => x.Id != null).ToDictionary(x => x.Id);
            return _items;
        }
        else
        {
            _logger.LogError("Something went wrong while fetching the items from the hypixel api");
            return null;
        }
    }

    public async Task<IEnumerable<Cost>> GetSlotCost(string itemId, List<string> pexiting, List<string> result)
    {
        // slots can be called the same but be more expensive
        // e.g. "COMBAT_0" and "COMBAT_1"
        var items = await GetItemsAsync();
        return await GetSlotCostSync(itemId, pexiting, result, items);
    }

    private async Task<IEnumerable<Cost>> GetSlotCostSync(string itemId, List<string> pexiting, List<string> result, Dictionary<string, Item> items = null)
    {
        items = items ?? _items;
        if(items == null)
            return new List<Cost>();
        
        var item = items[itemId];
        var costs = new List<Cost>();
        var newSlots = result.Except(pexiting);
        foreach (var slot in newSlots)
        {
            try
            {
                var index = slot.Last() - '0';
                var type = slot.Substring(0, slot.Length - 2);
                var slots = item.GemstoneSlots.Where(x => x.SlotType == type).Skip(index).FirstOrDefault();
                if (slots == null)
                {
                    _logger.LogWarning($"Failed to get slot costs for {itemId} {slot} {string.Join(", ", result)}");
                    continue;
                }
                costs.AddRange(slots.Costs);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to get slot costs for {itemId} {slot} {string.Join(", ", result)}");
                await Task.Delay(20000);
            }
        }
        return costs;
    }

    public async Task<IEnumerable<DungeonUpgradeCost>> GetStarCost(string itemId, int baseTier, int tier)
    {
        await GetItemsAsync();
        return GetStarCostSync(itemId, baseTier, tier);
    }

    private IEnumerable<DungeonUpgradeCost> GetStarCostSync(string itemId, int baseTier, int tier)
    {
        var items = _items;
        if (items == null)
            return new List<DungeonUpgradeCost>();
        if (baseTier >= 5) // master stars
            return new List<DungeonUpgradeCost>();
        var item = items[itemId];
        var cost = item.UpgradeCosts;
        if (cost == null)
        {
            _logger.LogWarning($"Failed to get salvage costs for {itemId} {tier}");
            return new List<DungeonUpgradeCost>();
        }

        return cost.Skip(baseTier).Take(tier - baseTier).SelectMany(x => x);
    }
}
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public record CatacombsRequirement(
      [property: JsonPropertyName("type")] string Type,
      [property: JsonPropertyName("dungeon_type")] string DungeonType,
      [property: JsonPropertyName("level")] int Level
  );

public record Cost(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("coins")] int Coins,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("amount")] int? Amount
);

public record DungeonItemConversionCost(
    [property: JsonPropertyName("essence_type")] string EssenceType,
    [property: JsonPropertyName("amount")] int Amount
);

public record Enchantments(
    [property: JsonPropertyName("counter_strike")] int CounterStrike
);

public record GemstoneSlot(
    [property: JsonPropertyName("slot_type")] string SlotType,
    [property: JsonPropertyName("costs")] IReadOnlyList<Cost> Costs
);

public record Item(
    [property: JsonPropertyName("material")] string Material,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("npc_sell_price")] double NpcSellPrice,
    [property: JsonPropertyName("tiered_stats")] TieredStats TieredStats,
    [property: JsonPropertyName("upgrade_costs")] IReadOnlyList<List<DungeonUpgradeCost>> UpgradeCosts,
    [property: JsonPropertyName("gear_score")] int GearScore,
    [property: JsonPropertyName("requirements")] IReadOnlyList<Requirement> Requirements,
    [property: JsonPropertyName("dungeon_item")] bool DungeonItem,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("glowing")] bool? Glowing,
    [property: JsonPropertyName("item_durability")] int? ItemDurability,
    [property: JsonPropertyName("stats")] Stats Stats,
    [property: JsonPropertyName("gemstone_slots")] IReadOnlyList<GemstoneSlot> GemstoneSlots,
    [property: JsonPropertyName("durability")] int? Durability,
    [property: JsonPropertyName("skin")] string Skin,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("unstackable")] bool? Unstackable,
    [property: JsonPropertyName("dungeon_item_conversion_cost")] DungeonItemConversionCost DungeonItemConversionCost,
    [property: JsonPropertyName("catacombs_requirements")] IReadOnlyList<CatacombsRequirement> CatacombsRequirements,
    [property: JsonPropertyName("museum")] bool? Museum,
    [property: JsonPropertyName("can_have_attributes")] bool? CanHaveAttributes,
    [property: JsonPropertyName("salvages")] IReadOnlyList<Salvage> Salvages,
    [property: JsonPropertyName("soulbound")] string Soulbound,
    [property: JsonPropertyName("furniture")] string Furniture,
    [property: JsonPropertyName("enchantments")] Enchantments Enchantments
);

public record Requirement(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("dungeon_type")] string DungeonType,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("tier")] int? Tier,
    [property: JsonPropertyName("skill")] string Skill,
    [property: JsonPropertyName("slayer_boss_type")] string SlayerBossType,
    [property: JsonPropertyName("reward")] string Reward
);

public record Root(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("lastUpdated")] long LastUpdated,
    [property: JsonPropertyName("items")] IReadOnlyList<Item> Items
);

public record Salvage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("essence_type")] string EssenceType,
    [property: JsonPropertyName("amount")] int Amount
);

public record DungeonUpgradeCost(
    [property: JsonPropertyName("essence_type")] string EssenceType,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("type")] string Type
);

public record Stats(
    [property: JsonPropertyName("DEFENSE")] int DEFENSE,
    [property: JsonPropertyName("HEALTH")] int HEALTH,
    [property: JsonPropertyName("INTELLIGENCE")] int INTELLIGENCE,
    [property: JsonPropertyName("WALK_SPEED")] int? WALKSPEED,
    [property: JsonPropertyName("DAMAGE")] int? DAMAGE,
    [property: JsonPropertyName("STRENGTH")] int? STRENGTH,
    [property: JsonPropertyName("SEA_CREATURE_CHANCE")] double? SEACREATURECHANCE,
    [property: JsonPropertyName("CRITICAL_DAMAGE")] int? CRITICALDAMAGE,
    [property: JsonPropertyName("CRITICAL_CHANCE")] int? CRITICALCHANCE
);

public record TieredStats(
    [property: JsonPropertyName("HEALTH")] IReadOnlyList<int> HEALTH,
    [property: JsonPropertyName("CRITICAL_CHANCE")] IReadOnlyList<int> CRITICALCHANCE,
    [property: JsonPropertyName("DEFENSE")] IReadOnlyList<int> DEFENSE,
    [property: JsonPropertyName("CRITICAL_DAMAGE")] IReadOnlyList<int> CRITICALDAMAGE,
    [property: JsonPropertyName("DAMAGE")] IReadOnlyList<int> DAMAGE,
    [property: JsonPropertyName("STRENGTH")] IReadOnlyList<int> STRENGTH,
    [property: JsonPropertyName("INTELLIGENCE")] IReadOnlyList<int> INTELLIGENCE,
    [property: JsonPropertyName("WALK_SPEED")] IReadOnlyList<int> WALKSPEED
);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member