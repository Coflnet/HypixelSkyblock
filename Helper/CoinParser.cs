using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Coflnet.Sky.Core;
public class CoinParser
{
    private static readonly Regex MinecraftFormattingRegex = new("§.", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public long GetCoinAmount(Item item)
    {
        if (IsCoins(item))
        {
            return ParseCoinAmount(ExtractAmountFromName(item.ItemName!));
        }
        return 0;
    }

    /// <summary>
    /// Extracts the numeric portion from a coin item name (e.g. "§67M coins" or "7M coins" => "7M").
    /// Strips minecraft formatting codes and the trailing " coins" suffix instead of relying on
    /// fixed offsets, which break when the color prefix is missing.
    /// </summary>
    private static string ExtractAmountFromName(string itemName)
    {
        var cleaned = MinecraftFormattingRegex.Replace(itemName, string.Empty);
        if (cleaned.EndsWith(" coins", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^" coins".Length];
        return cleaned.Trim();
    }

    public static long ParseCoinAmount(string stringAmount)
    {
        stringAmount = stringAmount.Trim();
        if (string.IsNullOrEmpty(stringAmount))
            return 0;

        double multiplier = 1;
        if (stringAmount.EndsWith("B"))
        {
            multiplier = 1_000_000_000;
            stringAmount = stringAmount[..^1];
        }
        else if (stringAmount.EndsWith("M"))
        {
            multiplier = 1_000_000;
            stringAmount = stringAmount[..^1];
        }
        else if (stringAmount.EndsWith("k"))
        {
            multiplier = 1_000;
            stringAmount = stringAmount[..^1];
        }

        // a bare suffix ("M", "k") or otherwise non-numeric text must not crash the caller
        if (!double.TryParse(stringAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            Console.WriteLine($"Failed to parse coins:`{stringAmount}`");
            return 0;
        }

        return (long)(parsed * multiplier * 10);
    }

    public long GetInventoryCoinSum(IEnumerable<Item> items)
    {
        var descriptions = items.Select(i => i.Description);
        if (TryParseFromDescription(descriptions, out var result))
        {
            return result;
        }
        return items.Sum(GetCoinAmount);
    }

    public static bool TryParseFromDescription(IEnumerable<string> descriptions, out long result)
    {
        var withSumary = descriptions.Where(i => i?.Contains("Total Coins Offered:") ?? false).FirstOrDefault();
        if (withSumary == null)
        {
            result = 0;
            return false;
        }
        // parse number from §8(1,500,000)
        var detailed = withSumary.Split('\n').Last().Substring(2).Trim('(', ')').Replace(",", "");
        if (long.TryParse(detailed, out var detailedAmount))
        {
            result = detailedAmount;
            return true;
        }
        result = ParseCoinAmount(withSumary.Split('\n').Skip(1).First().Substring(2));
        return true;
    }

    public bool IsCoins(Item item)
    {
        return item.ItemName?.EndsWith(" coins") ?? false;
    }
}

