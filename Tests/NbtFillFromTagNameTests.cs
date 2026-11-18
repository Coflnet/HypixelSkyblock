using Coflnet.Sky.Core;
using fNbt.Tags;
using NUnit.Framework;

namespace Tests;

public class NbtFillFromTagNameTests
{
    private static NbtCompound PlayerHead(NbtCompound customName)
    {
        var components = new NbtCompound("components") {
            new NbtCompound("minecraft:custom_data") { new NbtString("id", "INFERNAL_CRIMSON_HELMET") }
        };
        if (customName != null)
            components.Add(customName);
        return new NbtCompound {
            new NbtInt("count", 1),
            new NbtString("id", "minecraft:player_head"),
            components
        };
    }

    [Test]
    public void SingleCharacterNameDoesNotThrowInTierFallback()
    {
        var name = new NbtCompound("minecraft:custom_name") {
            new NbtByte("italic", 0), new NbtString("text", "j"), new NbtString("color", "green") };
        var item = PlayerHead(name);
        Assert.That(NBT.GetName(item), Is.EqualTo("j")); // top level color is not converted, name is 1 char

        var auction = new SaveAuction();
        Assert.DoesNotThrow(() => NBT.FillFromTag(auction, item, true));

        Assert.That(auction.Tag, Is.EqualTo("INFERNAL_CRIMSON_HELMET"));
        Assert.That(auction.ItemName, Is.EqualTo("j"));
        Assert.That(auction.Tier, Is.EqualTo(Tier.UNKNOWN));
    }

    [Test]
    public void MissingNameDoesNotThrowInTierFallback()
    {
        var auction = new SaveAuction();
        Assert.DoesNotThrow(() => NBT.FillFromTag(auction, PlayerHead(null), true));
        Assert.That(auction.Tier, Is.EqualTo(Tier.UNKNOWN));
    }

    [Test]
    public void ColoredNameStillSetsTierFromPrefix()
    {
        var name = new NbtCompound("minecraft:custom_name") { new NbtString("text", "§4Skin") };
        var auction = new SaveAuction();
        NBT.FillFromTag(auction, PlayerHead(name), true);
        Assert.That(auction.Tier, Is.EqualTo(Tier.ULTIMATE));
    }
}
