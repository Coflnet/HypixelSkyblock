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

    }
}
