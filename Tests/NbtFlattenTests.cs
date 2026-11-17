using System.Collections.Generic;
using System.Linq;
using Coflnet.Sky.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Tests
{
    /// <summary>
    /// Inputs shaped like the messagepack contractless deserialization in player-state:
    /// nested maps are Dictionary&lt;object,object&gt; and lists are object[]
    /// </summary>
    public class NbtFlattenTests
    {
        private static Dictionary<object, object> Map(params (string, object)[] entries)
            => entries.ToDictionary(e => (object)e.Item1, e => e.Item2);

        private static Dictionary<string, object> Flat(Dictionary<string, object> data)
            => NBT.FlattenNbtData(data).ToDictionary(k => k.Key, k => k.Value);

        [Test]
        public void PetInfoAsObjectDictionaryIsFlattened()
        {
            var data = new Dictionary<string, object>
            {
                ["petInfo"] = Map(
                    ("type", "JADE_DRAGON"), ("active", false), ("exp", 252219686.27), ("tier", "LEGENDARY"),
                    ("heldItem", "PET_ITEM_SPOOKY_CUPCAKE"), ("uuid", "a6bb"), ("candyUsed", 0),
                    ("extraData", Map(("foo", 3))), ("petSoulbound", false))
            };

            var result = Flat(data);

            ClassicAssert.AreEqual("JADE_DRAGON", result["type"]);
            ClassicAssert.AreEqual("LEGENDARY", result["tier"]);
            ClassicAssert.AreEqual("PET_ITEM_SPOOKY_CUPCAKE", result["heldItem"]);
            ClassicAssert.AreEqual(3L, result["foo"]);
            ClassicAssert.IsFalse(result.ContainsKey("uuid"));
            ClassicAssert.IsFalse(result.ContainsKey("petInfo"));
        }

        private static Dictionary<string, object> EffectsListShape() => new()
        {
            ["potion_level"] = 1,
            ["effects"] = new List<object>
            {
                new Dictionary<string, object> { ["modifiers"] = new Dictionary<string, object>(), ["effect"] = "blindness", ["duration_ticks"] = 90, ["level"] = 1 }
            },
            ["potion"] = "blindness"
        };

        [Test]
        public void EffectsListObjectShapeIsFlattened()
        {
            var result = Flat(EffectsListShape());

            ClassicAssert.AreEqual("blindness", result["effect"]);
            ClassicAssert.AreEqual(1, result["level"]);
            ClassicAssert.AreEqual(90, result["duration_ticks"]);
            ClassicAssert.IsFalse(result.ContainsKey("effects"));
        }

        [Test]
        public void EffectsMessagePackShapeMatchesListShape()
        {
            var data = new Dictionary<string, object>
            {
                ["potion_level"] = 1,
                ["effects"] = new object[] { Map(("modifiers", Map()), ("effect", "blindness"), ("duration_ticks", 90), ("level", 1)) },
                ["potion"] = "blindness"
            };

            var result = Flat(data);

            CollectionAssert.AreEquivalent(Flat(EffectsListShape()), result);
        }

        private static Dictionary<string, object> SoulsData() => new()
        {
            ["tier"] = 5,
            ["necromancer_souls"] = new object[]
            {
                Map(("dropped_mode_id", "crimson_isle"), ("mob_id", "WITHER_SPECTRE_70")),
                Map(("dropped_mode_id", "crimson_isle"), ("mob_id", "WITHER_SPECTRE_70")),
                Map(("dropped_mode_id", "hub"), ("mob_id", "GRAVEYARD_ZOMBIE_1")),
            }
        };

        [Test]
        public void NecromancerSoulsMessagePackShapeAreCounted()
        {
            var data = SoulsData();
            var result = Flat(data);

            ClassicAssert.AreEqual(2, result["WITHER_SPECTRE_70"]);
            ClassicAssert.AreEqual(1, result["GRAVEYARD_ZOMBIE_1"]);
            ClassicAssert.IsFalse(result.ContainsKey("necromancer_souls"));
        }

        [Test]
        public void NecromancerSoulsWithBoxedLongCountDoNotThrow()
        {
            var data = SoulsData();
            data["WITHER_SPECTRE_70"] = 5L;

            var result = Flat(data);

            ClassicAssert.AreEqual(7, result["WITHER_SPECTRE_70"]);
        }

        [Test]
        public void VinylsDoNotCreatePerUuidKeys()
        {
            var vinyls = new Dictionary<string, object>();
            for (var i = 0; i < 20; i++)
                vinyls[System.Guid.NewGuid().ToString()] = 1;
            var data = new Dictionary<string, object> { ["vinyls"] = vinyls, ["other"] = 5 };

            var result = Flat(data);

            ClassicAssert.AreEqual(1, result.Count);
            ClassicAssert.IsTrue(result.ContainsKey("other"));
        }

        [TestCase("builder's_wand_data")]
        [TestCase("builder's_ruler_data")]
        [TestCase("new_year_cake_bag_data")]
        [TestCase("frosty_the_snow_blaster_data")]
        [TestCase("frosty_the_snow_cannon_data")]
        [TestCase("greater_backpack_data")]
        [TestCase("jumbo_backpack_data")]
        [TestCase("large_backpack_data")]
        [TestCase("medium_backpack_data")]
        [TestCase("small_backpack_data")]
        public void BlobKeysAreNotIndexed(string key)
        {
            var data = new Dictionary<string, object> { [key] = "[1,2,3,4]", ["other"] = 5 };

            var remaining = NBT.WithoutBlobKeys(NBT.FlattenNbtData(data));

            CollectionAssert.AreEqual(new[] { "other" }, remaining.Select(r => r.Key).ToArray());
        }

        [Test]
        public void CreateLookupSkipsBlobKeysBeforeResolvingIds()
        {
            // a blob only input never reaches the key id resolution (which needs the db)
            var data = new Dictionary<string, object> { ["jumbo_backpack_data"] = "[1,2,3,4]" };

            var lookups = new NBT(null).CreateLookup("TEST", data);

            ClassicAssert.IsEmpty(lookups);
        }

        [Test]
        public void PetExtraDataNullValueIsOmitted()
        {
            var data = new Dictionary<string, object>
            {
                ["petInfo"] = Map(("type", "X"), ("extraData", Map(("good", 3), ("nothing", null))))
            };

            var result = Flat(data);

            ClassicAssert.AreEqual(3L, result["good"]);
            ClassicAssert.IsFalse(result.ContainsKey("nothing"));
        }

        [Test]
        public void PetExtraDataNonNumericDoesNotThrow()
        {
            var data = new Dictionary<string, object>
            {
                ["petInfo"] = Map(("type", "X"), ("extraData", Map(("good", 3), ("bad", "abc"), ("str", "7"))))
            };

            Dictionary<string, object> result = null;
            Assert.DoesNotThrow(() => result = Flat(data));

            ClassicAssert.AreEqual(3L, result["good"]);
            ClassicAssert.AreEqual(7L, result["str"]);
            ClassicAssert.IsFalse(result.ContainsKey("bad"));
        }
    }
}
