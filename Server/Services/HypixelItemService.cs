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
    private HashSet<string> IsDungeonItem = new();

    public HypixelItemService(HttpClient httpClient, ILogger<HypixelItemService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, Item>> GetItemsAsync()
    {
        if (_items != null)
            return _items;

        _items = await LoadItems();
        IsDungeonItem = new HashSet<string>(_items.Values.Where(x => x.DungeonItem).Select(x => x.Id));
        return _items;
    }

    private async Task<Dictionary<string, Item>> LoadItems()
    {
        if (File.Exists("items.json"))
        {
            _items = JsonSerializer.Deserialize<Root>(await File.ReadAllTextAsync("items.json"))
                .Items.Where(x => x.Id != null).ToDictionary(x => x.Id);
            return _items;
        }
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

    /// <summary>
    /// Check if the item is a dungeon item
    /// </summary>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public bool IsDungeonItemSync(string itemId)
    {
        return IsDungeonItem.Contains(itemId);
    }

    public async Task<IEnumerable<Cost>> GetSlotCost(string itemId, List<string> pexiting, List<string> result)
    {
        // slots can be called the same but be more expensive
        // e.g. "COMBAT_0" and "COMBAT_1"
        var items = await GetItemsAsync();
        return GetSlotCostSync(itemId, pexiting, result, items).Item1;
    }

    public (IEnumerable<Cost>, List<string> unavailable) GetSlotCostSync(string itemId, List<string> pexiting, List<string> result, Dictionary<string, Item> items = null)
    {
        items = items ?? _items;
        if (items == null || itemId == null || !items.TryGetValue(itemId, out var item))
            return (new List<Cost>(), new List<string>());

        var costs = new List<Cost>();
        var unavailable = new List<string>();
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
                    if (index <= 2 && Random.Shared.NextDouble() < 0.05)
                        _logger.LogWarning($"Failed to get slot costs for {itemId} {slot} {string.Join(", ", result)}");
                    unavailable.Add(slot);
                    continue;
                }
                if (slots.Costs == null)
                {
                    continue;
                }
                costs.AddRange(slots.Costs);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, $"Failed to get slot costs for {itemId} {slot} {string.Join(", ", result)}");
            }
        }
        return (costs, unavailable);
    }

    public (string color,string category) GetDefaultColorAndCategory(string itemId)
    {
        var items = _items;
        if (items == null || itemId == null || !items.TryGetValue(itemId, out var item))
            return (null, null);
        return (item.Color, item.Category);
    }

    public IEnumerable<string> GetUnlockableSlots(string itemId)
    {
        var items = _items;
        if (items == null || itemId == null || !items.TryGetValue(itemId, out var item))
            return new List<string>();
        if (item.GemstoneSlots == null)
            return new List<string>();
        var allSlots = item.GemstoneSlots.Where(x => x.Costs != null).Select(x => x.SlotType);
        // index distinct types with _x
        var distinctSlots = allSlots.Distinct().SelectMany(x => Enumerable.Range(0, allSlots.Count(y => y == x)).Select(y => $"{x}_{y}"));
        return distinctSlots;
    }

    public async Task<IEnumerable<DungeonUpgradeCost>> GetStarCost(string itemId, int baseTier, int tier)
    {
        await GetItemsAsync();
        return GetStarCostSync(itemId, baseTier, tier);
    }

    public IEnumerable<(string itemId, int amount)> GetStarIngredients(string itemId, int tier)
    {
        foreach (var item in GetStarCostSync(itemId, 0, tier))
        {
            if (item.Type == "ESSENCE")
                yield return ($"ESSENCE_{item.EssenceType}", item.Amount);
            else if (item.Type == "ITEM")
                yield return (item.ItemId, item.Amount);
        }
    }

    private IEnumerable<DungeonUpgradeCost> GetStarCostSync(string itemId, int baseTier, int tier)
    {
        var items = _items;
        if (items == null)
            return new List<DungeonUpgradeCost>();
        if (baseTier >= 5) // master stars
            return new List<DungeonUpgradeCost>();
        if (!items.TryGetValue(itemId, out var item))
            return new List<DungeonUpgradeCost>();
        var cost = item.UpgradeCosts;
        if (cost == null)
        {
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
    //[property: JsonPropertyName("skin")] string Skin, not needed, save ram
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