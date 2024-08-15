/*
 * Based on the GPL-2.0 licensed file https://github.com/TGWaffles/iTEM/blob/master/src/main/java/club/thom/tem/constants/FairyColours.java
 */
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Coflnet.Sky.Core.Services;

public class FairyColors {
    public static readonly ImmutableHashSet<string> fairyColourConstants = [
            "330066", "4C0099", "660033", "660066", "6600CC", "7F00FF", "99004C", "990099", "9933FF", "B266FF",
            "CC0066", "CC00CC", "CC99FF", "E5CCFF", "FF007F", "FF00FF", "FF3399", "FF33FF", "FF66B2", "FF66FF",
            "FF99CC", "FF99FF", "FFCCE5", "FFCCFF"
    ];

    public static readonly ImmutableHashSet<string> ogFairyColourConstants = [
            "FF99FF", "FFCCFF", "E5CCFF", "CC99FF", "CC00CC", "FF00FF", "FF33FF", "FF66FF",
            "B266FF", "9933FF", "7F00FF", "660066", "6600CC", "4C0099", "330066", "990099"
    ];

    public static readonly ImmutableHashSet<string> ogFairyColourBootsExtras = [
            "660033", "99004C", "CC0066"
    ];

    public static readonly ImmutableHashSet<string> ogFairyColourLeggingsExtras = [
            "660033", "99004C", "FFCCE5"
    ];

    public static readonly ImmutableHashSet<string> ogFairyColourChestplateExtras = [
            "660033", "FFCCE5", "FF99CC"
    ];

    public static readonly ImmutableHashSet<string> ogFairyColourHelmetExtras = [
            "FFCCE5", "FF99CC", "FF66B2"
    ];

    public static bool IsFairyColor(string hex) {
        return fairyColourConstants.Contains(hex.ToUpper());
    }

    public static bool IsOgFairy(string itemId, string category, string hex) {
        hex = hex.ToUpper();
        if (ogFairyColourConstants.Contains(hex)) {
            return true;
        }

        if (itemId.Contains("BOOTS") || category.Equals("BOOTS")) {
            return ogFairyColourBootsExtras.Contains(hex);
        }

        if (itemId.Contains("LEGGINGS") || category.Equals("LEGGINGS")) {
            return ogFairyColourLeggingsExtras.Contains(hex);
        }

        if (itemId.Contains("CHESTPLATE") || category.Equals("CHESTPLATE")) {
            return ogFairyColourChestplateExtras.Contains(hex);
        }

        if (itemId.Contains("HELMET") || category.Equals("HELMET")) {
            return ogFairyColourHelmetExtras.Contains(hex);
        }

        return false;
    }
}

public class GlitchedColours {
    // 20th of November 2020
    private static readonly long GLITCHED_AFTER_DATE = 1605830400000L;

    public static readonly ImmutableDictionary<string, string> OTHER_GLITCHED = new Dictionary<string, string> {
           { "FFDC51", "SHARK_SCALE"},
           { "F7DA33", "FROZEN_BLAZE"},
           { "606060", "BAT_PERSON"}
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, string> CHESTPLATE_COLOURS = new Dictionary<string, string> {
            { "E7413C", "POWER_WITHER_CHESTPLATE"},
            { "45413C", "TANK_WITHER_CHESTPLATE"},
            { "4A14B7", "SPEED_WITHER_CHESTPLATE"},
            { "1793C4", "WISE_WITHER_CHESTPLATE"},
            { "000000", "WITHER_CHESTPLATE"}
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, string> LEGGINGS_COLOURS = new Dictionary<string, string> {
            { "E75C3C", "POWER_WITHER_LEGGINGS"},
            { "65605A", "TANK_WITHER_LEGGINGS"},
            { "5D2FB9", "SPEED_WITHER_LEGGINGS"},
            { "17A8C4", "WISE_WITHER_LEGGINGS"},
            { "000000", "WITHER_LEGGINGS"}
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, string> BOOT_COLOURS = new Dictionary<string, string> {
            { "E76E3C", "POWER_WITHER_BOOTS"},
            { "88837E", "TANK_WITHER_BOOTS"},
            { "8969C8", "SPEED_WITHER_BOOTS"},
            { "1CD4E4", "WISE_WITHER_BOOTS"},
            { "000000", "WITHER_BOOTS"}
    }.ToImmutableDictionary();

    public static bool isTooOld(long creationTimestamp) {
        return creationTimestamp < GLITCHED_AFTER_DATE;
    }

    public static bool isGlitched(string itemId, string hex, long creationTimestamp) {
        if (itemId.Contains("WITHER")) {
            return checkWitherGlitched(itemId, hex, creationTimestamp);
        }
        string otherGlitchedItemId = OTHER_GLITCHED.GetValueOrDefault(hex);
        return otherGlitchedItemId != null && itemId.StartsWith(otherGlitchedItemId);
    }

    private static bool checkWitherGlitched(string itemId, string hex, long creationTimestamp) {
        if (hex.Equals("000000") && isTooOld(creationTimestamp)) {
            return false;
        }

        if (itemId.Contains("CHESTPLATE")) {
            return checkChestplateGlitched(itemId, hex);
        }
        if (itemId.Contains("LEGGINGS")) {
            return checkLeggingsGlitched(itemId, hex);
        }
        if (itemId.Contains("BOOTS")) {
            return checkBootsGlitched(itemId, hex);
        }
        return false;
    }

    private static bool checkChestplateGlitched(string itemId, string hex) {
        // hex is a chestplate hex and the type isn't the same as what it should be
        return CHESTPLATE_COLOURS.ContainsKey(hex) && CHESTPLATE_COLOURS.TryGetValue(itemId, out var val) && !val.Equals(itemId);
    }

    private static bool checkLeggingsGlitched(string itemId, string hex) {
        // hex is a leggings hex and the type isn't the same as what it should be
        return LEGGINGS_COLOURS.ContainsKey(hex) && LEGGINGS_COLOURS.TryGetValue(itemId, out var val) && !val.Equals(itemId);
    }

    private static bool checkBootsGlitched(string itemId, string hex) {
        // hex is a boots hex and the type isn't the same as what it should be
        return BOOT_COLOURS.ContainsKey(hex) && BOOT_COLOURS.TryGetValue(itemId, out var val) && !val.Equals(itemId);
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member