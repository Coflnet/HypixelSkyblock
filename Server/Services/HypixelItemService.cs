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

public interface IHypixelItemStore
{
    Task<Dictionary<string, Item>> GetItemsAsync();
}

public class HypixelItemService : IHypixelItemStore
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
        IsDungeonItem = new HashSet<string>(_items.Values.Where(x => x.DungeonItem || x.DungeonItemConversionCost != null).Select(x => x.Id));
        return _items;
    }

    private async Task<Dictionary<string, Item>> LoadItems()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new IntFromStringJsonConverter() }
        };
        if (File.Exists("items.json"))
        {
            _items = JsonSerializer.Deserialize<Root>(await File.ReadAllTextAsync("items.json"), options)
                .Items.Where(x => x.Id != null).ToDictionary(x => x.Id);
            return _items;
        }
        var response = await _httpClient.GetAsync("https://api.hypixel.net/resources/skyblock/items");
        if (response.IsSuccessStatusCode)
        {
            var responseStream = await response.Content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<Root>(responseStream, options);
            _items = data.Items.Where(x => x.Id != null).ToDictionary(x => x.Id);
            return _items;
        }
        else
        {
            _logger.LogError("Something went wrong while fetching the items from the hypixel api");
            return null;
        }
    }

    public class IntFromStringJsonConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var value))
            {
                return value;
            }
            return reader.GetInt32();
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
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
                var slots = item.GemstoneSlots?.Where(x => x.SlotType == type).Skip(index).FirstOrDefault();
                if (slots == null)
                {
                    var starredId = "STARRED_" + itemId;
                    if (items.TryGetValue(starredId, out var starredItem))
                    {
                        slots = starredItem.GemstoneSlots.Where(x => x.SlotType == type).Skip(index).FirstOrDefault();
                    }
                    else
                    {
                        if (index <= 2 && Random.Shared.NextDouble() < 0.05)
                            _logger.LogWarning($"Failed to get slot costs for {itemId} {slot} {string.Join(", ", result)} i {index} t {type}");
                        unavailable.Add(slot);
                        continue;
                    }
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

    private static readonly Dictionary<string, string[]> ExtraItemsRequired = new()
    {
        { "CRIMSON_HUNTER", [ "BLAZE_BELT"] },
        // ^ items with multiple sets
        {"SNOW_SUIT", ["SNOW_NECKLACE", "SNOW_CLOAK", "SNOW_BELT", "SNOW_GLOVES"] },
    };

    public Dictionary<string, HashSet<string>> GetArmorSets()
    {
        var general = _items.Values.Where(i => i.MuseumData?.ArmorSetDonationXp != null && i.MuseumData.ArmorSetDonationXp?.Count != 0)
                .SelectMany(i => i.MuseumData.ArmorSetDonationXp.Select(aset => (i.Id, aset.Key)))
                .GroupBy(i => i.Key) // there are 14 items that are part of multiple sets
                .ToDictionary(i => i.Key,
                    i => i.Select(i => i.Id).ToHashSet(), StringComparer.OrdinalIgnoreCase);
        foreach (var (item, sets) in ExtraItemsRequired)
        {
            if (general.TryGetValue(item, out var set))
                foreach (var toAdd in sets)
                {
                    set.Add(toAdd);
                }
        }
        return general;
    }

    public (string color, string category) GetDefaultColorAndCategory(string itemId)
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
        if (!items.TryGetValue(itemId, out var item))
            return new List<DungeonUpgradeCost>();
        var cost = item.UpgradeCosts;
        if (cost == null)
        {
            return new List<DungeonUpgradeCost>();
        }
        var extra = new List<DungeonUpgradeCost>();
        if (tier > cost.Count)
        {
            // no tiers available to upgrade, assume master stars needed
            if (tier == 10 && baseTier <= 9)
                extra.Add(new DungeonUpgradeCost(null, 1, "FIFTH_MASTER_STAR", "ITEM"));
            if (tier >= 9 && baseTier <= 8)
                extra.Add(new DungeonUpgradeCost(null, 1, "FOURTH_MASTER_STAR", "ITEM"));
            if (tier >= 8 && baseTier <= 7)
                extra.Add(new DungeonUpgradeCost(null, 1, "THIRD_MASTER_STAR", "ITEM"));
            if (tier >= 7 && baseTier <= 6)
                extra.Add(new DungeonUpgradeCost(null, 1, "SECOND_MASTER_STAR", "ITEM"));
            if (tier >= 6 && baseTier <= 5)
                extra.Add(new DungeonUpgradeCost(null, 1, "FIRST_MASTER_STAR", "ITEM"));
        }

        return cost.Skip(baseTier).Take(tier - baseTier).SelectMany(x => x).Concat(extra);
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
    [property: JsonPropertyName("prestige")] Prestige prestige,
    //[property: JsonPropertyName("skin")] string Skin, not needed, save ram
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("unstackable")] bool? Unstackable,
    [property: JsonPropertyName("dungeon_item_conversion_cost")] DungeonItemConversionCost DungeonItemConversionCost,
    [property: JsonPropertyName("catacombs_requirements")] IReadOnlyList<CatacombsRequirement> CatacombsRequirements,
    [property: JsonPropertyName("museum_data")] MuseumData MuseumData,
    [property: JsonPropertyName("can_have_attributes")] bool? CanHaveAttributes,
    [property: JsonPropertyName("salvages")] IReadOnlyList<Salvage> Salvages,
    [property: JsonPropertyName("soulbound")] string Soulbound,
    [property: JsonPropertyName("furniture")] string Furniture,
    [property: JsonPropertyName("enchantments")] Enchantments Enchantments
);

public record Prestige(
    [property: JsonPropertyName("item_id")] string item_id,
    [property: JsonPropertyName("costs")] IReadOnlyList<DungeonUpgradeCost> costs
);

public record MuseumData(
    [property: JsonPropertyName("donation_xp")] int DonationXp,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("parent")] Dictionary<string, string> Parent,
    [property: JsonPropertyName("mapped_item_ids")] IReadOnlyList<string> MappedItemIds,
    [property: JsonPropertyName("armor_set_donation_xp")] Dictionary<string, int> ArmorSetDonationXp,
    [property: JsonPropertyName("game_stage")] string GameStage
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