/*
 * Based on the GPL-2.0 licensed file https://github.com/TGWaffles/iTEM/blob/master/src/main/java/club/thom/tem/constants/VariantColours.java
 */
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Coflnet.Sky.Core.Services;

public class VariantColors {
    public static readonly Dictionary<string, ImmutableHashSet<string>> variants = getPossibleVariants();

    public static readonly ImmutableHashSet<string> seymourItems = [
            "VELVET_TOP_HAT",
            "CASHMERE_JACKET",
            "SATIN_TROUSERS",
            "OXFORD_SHOES"
    ];

    private static Dictionary<string, ImmutableHashSet<string>> getPossibleVariants() {
        Dictionary<string, ImmutableHashSet<string>> possibleVariants = new()
        {
            // they changed the colour of ranchers boots
            { "RANCHERS_BOOTS", ["CC5500", "000000"] },

            // reaper armour turns red!
            { "REAPER_BOOTS", ["1B1B1B", "FF0000"] },
            { "REAPER_LEGGINGS", ["1B1B1B", "FF0000"] },
            { "REAPER_CHESTPLATE", ["1B1B1B", "FF0000"] }
        };

        // adaptive changes based on class
        ImmutableHashSet<string> adaptiveChestPlate = ["3ABE78", "82E3D8", "BFBCB2", "D579FF", "FF4242", "FFC234"];
        ImmutableHashSet<string> adaptiveRest = ["169F57", "2AB5A5", "6E00A0", "BB0000", "BFBCB2", "FFF7E6"];
        possibleVariants.Add("STARRED_ADAPTIVE_CHESTPLATE", adaptiveChestPlate);
        possibleVariants.Add("ADAPTIVE_CHESTPLATE", adaptiveChestPlate);
        possibleVariants.Add("STARRED_ADAPTIVE_LEGGINGS", adaptiveRest);
        possibleVariants.Add("ADAPTIVE_LEGGINGS", adaptiveRest);
        possibleVariants.Add("STARRED_ADAPTIVE_BOOTS", adaptiveRest);
        possibleVariants.Add("ADAPTIVE_BOOTS", adaptiveRest);
        // ^^ END OF ADAPTIVE

        // Kuudra Follower Armour (Hypixel didn't feel like adding this to API)
        possibleVariants.Add("KUUDRA_FOLLOWER_CHESTPLATE", ["35530A"]);
        possibleVariants.Add("KUUDRA_FOLLOWER_LEGGINGS", ["35530A"]);
        possibleVariants.Add("KUUDRA_FOLLOWER_BOOTS", ["35530A"]);
        // ^^ END OF Kuudra Follower Armour


        return possibleVariants;
    }

    public static bool isVariantColour(string itemId, string hexCode) {
        if (itemId.StartsWith("FAIRY")) {
            return FairyColors.IsFairyColor(hexCode);
        }
        if (itemId.StartsWith("CRYSTAL")) {
            return ExoticColorService.crystalColours.Contains(hexCode);
        }
        if (itemId.StartsWith("LEATHER")) {
            return true;
        }
        if (itemId.Equals("GHOST_BOOTS")) {
            return true;
        }
        if (seymourItems.Contains(itemId)) {
            return true;
        }
        if (!variants.TryGetValue(itemId, out var possibleColoursForItem)) {
            return false;
        }
        return possibleColoursForItem.Contains(hexCode.ToUpper());
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member