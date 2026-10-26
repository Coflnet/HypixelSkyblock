using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class ActiveAuctionSnapshotIndexesTests
    {
        private static SaveAuction Make(long uid, Tier tier, ItemReferences.Reforge reforge, bool bin, bool color, params (Enchantment.EnchantmentType, byte)[] enchants)
        {
            var a = ActiveAuctionTestData.Make(uid);
            a.Tier = tier;
            a.Reforge = reforge;
            a.Bin = bin;
            a.Enchantments = enchants.Select(e => new Enchantment(e.Item1, e.Item2)).ToList();
            a.FlatenedNBT = color ? new Dictionary<string, string> { ["color"] = "1:2:3" } : new Dictionary<string, string>();
            return a;
        }

        private static ActiveAuctionSet Build()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(30, Tier.EPIC, ItemReferences.Reforge.Sharp, true, false, (Enchantment.EnchantmentType.sharpness, 5)), "t");
            set.Upsert(Make(10, Tier.LEGENDARY, ItemReferences.Reforge.Sharp, false, true, (Enchantment.EnchantmentType.sharpness, 7), (Enchantment.EnchantmentType.looting, 3)), "t");
            set.Upsert(Make(20, Tier.EPIC, ItemReferences.Reforge.None, true, true, (Enchantment.EnchantmentType.looting, 1)), "t");
            set.Upsert(Make(40, Tier.COMMON, ItemReferences.Reforge.None, false, false), "t");
            return set;
        }

        private static long[] Uids(ActiveAuctionSnapshot s, int[] indices) => indices.Select(i => s.All[i].UId).ToArray();

        [Test]
        public void ByColorValueGroupsSortedIndicesByParsedColor()
        {
            var set = new ActiveAuctionSet();
            foreach (var (uid, color) in new[] { (30L, "1:2:3"), (10L, "1:2:3"), (20L, "9:9:9"), (40L, null) })
            {
                var a = ActiveAuctionTestData.Make(uid);
                a.FlatenedNBT = color == null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["color"] = color };
                set.Upsert(a, "t");
            }
            var s = set.GetFreshSnapshot();
            Assert.That(s.ByColorValue.Count, Is.EqualTo(2));
            Assert.That(Uids(s, s.ByColorValue[NBT.GetColor("1:2:3")]), Is.EqualTo(new long[] { 10, 30 }));
            Assert.That(Uids(s, s.ByColorValue[NBT.GetColor("9:9:9")]), Is.EqualTo(new long[] { 20 }));
        }

        [Test]
        public void IndexesContainExpectedAuctionsSortedAscending()
        {
            var s = Build().GetFreshSnapshot();
            Assert.That(s.All.Select(a => a.UId), Is.EqualTo(new long[] { 10, 20, 30, 40 }));
            Assert.That(Uids(s, s.ByEnchantType[(byte)Enchantment.EnchantmentType.sharpness]), Is.EqualTo(new long[] { 10, 30 }));
            Assert.That(Uids(s, s.ByEnchantType[(byte)Enchantment.EnchantmentType.looting]), Is.EqualTo(new long[] { 10, 20 }));
            Assert.That(s.ByEnchantType.ContainsKey((byte)Enchantment.EnchantmentType.smite), Is.False);
            Assert.That(Uids(s, s.ByTier[Tier.EPIC]), Is.EqualTo(new long[] { 20, 30 }));
            Assert.That(Uids(s, s.ByTier[Tier.LEGENDARY]), Is.EqualTo(new long[] { 10 }));
            Assert.That(Uids(s, s.ByReforge[ItemReferences.Reforge.Sharp]), Is.EqualTo(new long[] { 10, 30 }));
            Assert.That(Uids(s, s.ByReforge[ItemReferences.Reforge.None]), Is.EqualTo(new long[] { 20, 40 }));
            Assert.That(Uids(s, s.BinOnly), Is.EqualTo(new long[] { 20, 30 }));
            Assert.That(Uids(s, s.WithColor), Is.EqualTo(new long[] { 10, 20 }));
            foreach (var list in s.ByEnchantType.Values.Concat(s.ByTier.Values).Concat(s.ByReforge.Values).Append(s.BinOnly).Append(s.WithColor))
                Assert.That(list, Is.Ordered.Ascending);
        }

        [Test]
        public void IndexesFollowRemovalsAndUpserts()
        {
            var set = Build();
            set.SnapshotMinInterval = TimeSpan.Zero;
            set.GetFreshSnapshot();
            set.Remove(10, RemoveReason.Sold);
            set.Upsert(Make(5, Tier.EPIC, ItemReferences.Reforge.Sharp, true, true, (Enchantment.EnchantmentType.sharpness, 2)), "t");
            var s = set.GetFreshSnapshot();
            Assert.That(s.All.Select(a => a.UId), Is.EqualTo(new long[] { 5, 20, 30, 40 }));
            Assert.That(Uids(s, s.ByEnchantType[(byte)Enchantment.EnchantmentType.sharpness]), Is.EqualTo(new long[] { 5, 30 }));
            Assert.That(Uids(s, s.ByEnchantType[(byte)Enchantment.EnchantmentType.looting]), Is.EqualTo(new long[] { 20 }));
            Assert.That(s.ByTier.ContainsKey(Tier.LEGENDARY), Is.False);
            Assert.That(Uids(s, s.BinOnly), Is.EqualTo(new long[] { 5, 20, 30 }));
            Assert.That(Uids(s, s.WithColor), Is.EqualTo(new long[] { 5, 20 }));
            Assert.That(Uids(s, s.BySeller.Values.SelectMany(x => x).OrderBy(i => i).ToArray()), Is.EqualTo(new long[] { 5, 20, 30, 40 }));
        }
    }
}
