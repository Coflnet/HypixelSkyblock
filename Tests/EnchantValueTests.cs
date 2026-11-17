using System.Collections.Generic;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests;

/// <summary>
/// Enchant levels without a book are applied with an upgrade item to the level below, so they are worth both.
/// </summary>
public class EnchantValueTests
{
    private readonly PropertyMapper mapper = new();

    [TestCase(Enchantment.EnchantmentType.forest_pledge, 6, "WATER_HYACINTH")]
    [TestCase(Enchantment.EnchantmentType.smite, 7, "SEVERED_HAND")]
    [TestCase(Enchantment.EnchantmentType.bane_of_arthropods, 7, "ENSNARED_SNAIL")]
    [TestCase(Enchantment.EnchantmentType.frail, 7, "SEVERED_PINCER")]
    [TestCase(Enchantment.EnchantmentType.spiked_hook, 7, "OCTOPUS_TENDRIL")]
    [TestCase(Enchantment.EnchantmentType.piscary, 7, "TROUBLED_BUBBLE")]
    [TestCase(Enchantment.EnchantmentType.luck_of_the_sea, 7, "GOLD_BOTTLE_CAP")]
    public void UpgradeItemLevelIsItemPlusLevelBelow(Enchantment.EnchantmentType type, int level, string upgradeItem)
    {
        var prices = new Dictionary<string, double>
        {
            [upgradeItem] = 1_000_000,
            [BookId(type, level - 1)] = 1_500_000
        };

        Assert.That(Value(type, level, prices), Is.EqualTo(2_500_000));
    }

    /// <summary>
    /// The level 1 book price is present so that a missing upgrade item can not fall through to the generic scaling
    /// from level 1, which would return a price.
    /// </summary>
    [TestCase(Enchantment.EnchantmentType.forest_pledge, 6)]
    [TestCase(Enchantment.EnchantmentType.smite, 7)]
    [TestCase(Enchantment.EnchantmentType.bane_of_arthropods, 7)]
    [TestCase(Enchantment.EnchantmentType.frail, 7)]
    [TestCase(Enchantment.EnchantmentType.spiked_hook, 7)]
    [TestCase(Enchantment.EnchantmentType.piscary, 7)]
    [TestCase(Enchantment.EnchantmentType.luck_of_the_sea, 7)]
    public void UpgradeItemLevelWithoutUpgradeItemPriceIsUnknown(Enchantment.EnchantmentType type, int level)
    {
        var prices = new Dictionary<string, double>
        {
            [BookId(type, 1)] = 10_000,
            [BookId(type, level - 1)] = 1_500_000
        };

        Assert.That(Value(type, level, prices), Is.EqualTo(-1));
    }

    [Test]
    public void ForestPledgeSixWithoutLevelBelowPriceIsUnknown()
    {
        var prices = new Dictionary<string, double> { ["WATER_HYACINTH"] = 1_000_000 };

        Assert.That(Value(Enchantment.EnchantmentType.forest_pledge, 6, prices), Is.EqualTo(-1));
    }

    [Test]
    public void BookOfTheLevelIsPreferredOverUpgradeItem()
    {
        var prices = new Dictionary<string, double>
        {
            ["ENCHANTMENT_SMITE_7"] = 4_000_000,
            ["SEVERED_HAND"] = 7_000_000,
            ["ENCHANTMENT_SMITE_6"] = 25_000
        };

        Assert.That(Value(Enchantment.EnchantmentType.smite, 7, prices), Is.EqualTo(4_000_000));
    }

    private long Value(Enchantment.EnchantmentType type, int level, Dictionary<string, double> prices)
    {
        return mapper.EnchantValue(new Enchantment(type, (byte)level), null, prices, "ANY_ITEM");
    }

    private static string BookId(Enchantment.EnchantmentType type, int level)
    {
        return $"ENCHANTMENT_{type.ToString().ToUpper()}_{level}";
    }
}
