using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Core.Services;

public class ExoticColorService
{
    private HypixelItemService itemService;
    private ILogger<ExoticColorService> logger;

    public ExoticColorService(HypixelItemService itemService, ILogger<ExoticColorService> logger)
    {
        this.itemService = itemService;
        this.logger = logger;
    }
    // immutable hashset
    private static readonly ImmutableHashSet<string> spookColours = [
            "000000", "070008", "0E000F", "150017", "1B001F", "220027", "29002E", "300036", "37003E", "3E0046",
            "45004D", "4C0055", "52005D", "590065", "60006C", "670074", "6E007C", "750084", "7C008B", "830093",
            "89009B", "9000A3", "9700AA", "993399", "9E00B2"];

    public static readonly ImmutableHashSet<string> crystalColours = [
            "1F0030", "46085E", "54146E", "5D1C78", "63237D", "6A2C82", "7E4196", "8E51A6", "9C64B3", "A875BD",
            "B88BC9", "C6A3D4", "D9C1E3", "E5D1ED", "EFE1F5", "FCF3FF"];
    public bool IsOriginal(string itemId, string hexCode, string originalHex)
    {
        if (itemId.StartsWith("GREAT_SPOOK"))
        {
            return spookColours.Contains(hexCode);
        }
        if (VariantColors.isVariantColour(itemId, hexCode))
        {
            return true;
        }
        return hexCode.Equals(originalHex);
    }
    public ExoticColorType GetExoticColorType(string itemId, string hexCode, long creationTime)
    {
        hexCode = hexCode.ToUpper();
        (var originalHex, var category) = itemService.GetDefaultColorAndCategory(itemId);
        if (IsOriginal(itemId, hexCode, originalHex))
        {
            return ExoticColorType.ORIGINAL;
        }

        if (FairyColors.IsOgFairy(itemId, category, hexCode))
        {
            return ExoticColorType.OG_FAIRY;
        }
        if (FairyColors.IsFairyColor(hexCode))
        {
            return ExoticColorType.FAIRY;
        }
        if (hexCode.Equals("A06540") || hexCode.Equals("UNDYED"))
        {
            return ExoticColorType.UNDYED;
        }
        if (crystalColours.Contains(hexCode))
        {
            return ExoticColorType.CRYSTAL;
        }

        if (GlitchedColours.isGlitched(itemId, hexCode, creationTime))
        {
            return ExoticColorType.GLITCHED;
        }

        if (itemId.StartsWith("FAIRY_") && spookColours.Contains(hexCode))
        {
            return ExoticColorType.SPOOK;
        }

        return ExoticColorType.EXOTIC;
    }

   

    public enum ExoticColorType
    {
        DEFAULT,
        CRYSTAL,
        FAIRY,
        OG_FAIRY,
        UNDYED,
        ORIGINAL,
        EXOTIC,
        GLITCHED,
        SPOOK,
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member