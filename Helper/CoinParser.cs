using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Coflnet.Sky.Core;
public class CoinParser
{
    public long GetCoinAmount(Item item)
    {
        if (IsCoins(item))
        {
            var stringAmount = item.ItemName!.Substring(2, item.ItemName.Length - 8);
            return ParseCoinAmount(stringAmount);
        }
        return 0;
    }

    public static long ParseCoinAmount(string stringAmount)
    {
        double parsed;
        stringAmount = stringAmount.Trim();
        try
        {
            if (stringAmount.EndsWith("B"))
                parsed = double.Parse(stringAmount.Trim('B'), CultureInfo.InvariantCulture) * 1_000_000_000;
            else if (stringAmount.EndsWith("M"))
                parsed = double.Parse(stringAmount.Trim('M'), CultureInfo.InvariantCulture) * 1_000_000;
            else if (stringAmount.EndsWith("k"))
                parsed = double.Parse(stringAmount.Trim('k'), CultureInfo.InvariantCulture) * 1_000;
            else
                parsed = double.Parse(stringAmount, CultureInfo.InvariantCulture);

            return (long)(parsed * 10);
        }
        catch (System.Exception)
        {
            Console.WriteLine($"Failed to parse coins:`{stringAmount}`");
            throw;
        }

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

