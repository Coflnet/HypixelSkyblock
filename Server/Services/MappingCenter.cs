using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Core.Services;

namespace Coflnet.Sky.Core;

public class MappingCenter
{
    public static PropertyMapper Mapper { get; } = new PropertyMapper();
    private HypixelItemService itemService;
    private readonly Func<string, Task<Dictionary<DateTime, long>>> priceGetter;
    private Dictionary<DateTime, Dictionary<string, double>> cachedPrices = new();
    private Dictionary<string, List<string>> ItemIngredient = new();

    public MappingCenter(HypixelItemService itemService, Func<string, Task<Dictionary<DateTime, long>>> priceGetter)
    {
        this.itemService = itemService;
        this.priceGetter = priceGetter;
    }

    public async Task Load()
    {
        await Mapper.LoadNeuConstants();
        await itemService.GetItemsAsync();
    }

    public IEnumerable<string> GetIngredients(string tag)
    {
        return ItemIngredient[tag];
    }

    public async Task<IEnumerable<(string, long count)>> GetItemsForProperty(string tag, string property, string value, Dictionary<string, string> flatNbt)
    {
        if (Mapper.TryGetIngredients(property, value, string.Empty, out var ingredients))
        {
            return ingredients.GroupBy(i => i).Select(g => (g.Key, (long)g.Count()));
        }
        if (property == "upgrade_level")
        {
            var cost = itemService.GetStarIngredients(tag, int.Parse(value));
            return cost.Select(c => (c.itemId, (long)c.amount));
        }
        if (value == "PERFECT" || value == "FLAWLESS" || value == "ROUGH" || value == "FINE")
        {
            var gemKey = Mapper.GetItemKeyForGem(new(tag, value), flatNbt);
            return [(gemKey, 1L)];
        }
        if (property == "unlocked_slots")
        {
            var cost = await itemService.GetSlotCost(tag, new(), value.Split(',').ToList());
            return cost.Select(c => c.Coins != 0 ? ("SKYBLOCK_COIN", (long)c.Coins) : (c.ItemId, (long)c.Amount));
        }

        return new List<(string, long)>();
    }

    public async Task<long> GetPriceForItemOn(string itemTag, DateTime date)
    {
        if (date.Date != date)
            date = date.Date;
        if (cachedPrices.TryGetValue(date, out var prices) && prices.TryGetValue(itemTag, out var priceOnDay))
            return (long)priceOnDay;

        var history = await priceGetter(itemTag);
        foreach (var item in history)
        {
            if (!cachedPrices.ContainsKey(item.Key))
                cachedPrices.Add(item.Key, new());
            var values = new List<long>();
            // add 2 days before and after and take median
            for (int i = -2; i <= 2; i++)
            {
                if (history.TryGetValue(item.Key.AddDays(i), out var value))
                {
                    values.Add(value);
                    continue;
                }
            }
            var median = values.OrderBy(v => v).ElementAt(values.Count / 2);
            cachedPrices[item.Key][itemTag] = median;
        }
        if (history.TryGetValue(date, out var price))
        {
            return price;
        }
        Console.WriteLine("No price found for " + itemTag + " on " + date);
        return 0;
    }

    public async Task<List<(string, long price)>> GetColumnsForAuction(SaveAuction auction, DateTime date)
    {
        List<(string, long)> columns = new();
        date = date.Date;
        var nbtItems = auction.FlatenedNBT.Select(kv => (kv, GetItemsForProperty(auction.Tag, kv.Key, kv.Value, auction.FlatenedNBT)));
        foreach (var item in nbtItems)
        {
            var priceSum = 0L;
            foreach (var (tag, count) in await item.Item2)
            {
                priceSum += count * await GetPriceForItemOn(tag, date);
            }
            columns.Add(($"{item.Item1.Key}:{item.Item1.Value}", priceSum));
        }
        foreach (var (e, item) in auction.Enchantments.Select(e => (e, Mapper.EnchantValue(e, auction.FlatenedNBT, cachedPrices.GetValueOrDefault(date, new())))))
        {
            if (item > 0)
            {
                columns.Add(($"!ench{e.Type}:{e.Level}", item));
                continue;
            }
            var key = $"ENCHANTMENT_{e.Type}_{e.Level}".ToUpper();
            var sum = await GetPriceForItemOn(key, date);
            await GetPriceForItemOn($"GOLDEN_BOUNTY".ToUpper(), date);
            await GetPriceForItemOn($"SIL_EX".ToUpper(), date);
            Console.WriteLine($"Enchantment value for {e.Type} {e.Level} was {item} vs {sum} from {key}");
            var value = Mapper.EnchantValue(e, auction.FlatenedNBT, cachedPrices.GetValueOrDefault(date, new()));
            columns.Add(($"!ench{e.Type}:{e.Level}", value));
            continue;
        }
        var reforgeCost = Mapper.GetReforgeCost(auction.Reforge);
        var coinValue = await GetPriceForItemOn(reforgeCost.Item1, date);
        columns.Add((auction.Reforge.ToString(), reforgeCost.Item2 + coinValue));
        foreach (var item in ItemIngredient[auction.Tag])
        {
            columns.Add((item, await GetPriceForItemOn(item, date)));
        }
        return columns;
    }

    public async Task SetIngredientsFor(string itemTag, List<string> ingredients)
    {
        ItemIngredient[itemTag] = ingredients;
        foreach (var item in ingredients)
        {
            await GetPriceForItemOn(item, DateTime.UtcNow.Date - TimeSpan.FromDays(1));
        }
    }
}
