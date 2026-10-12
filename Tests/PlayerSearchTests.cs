using System.Collections.Generic;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class PlayerSearchTests
    {
        [Test]
        public void UnknownPlayerWithForceResolutionThrows()
        {
            var result = new List<PlayerResult>();
            var ex = Assert.Throws<CoflnetException>(() => PlayerSearch.AddResolvedProfile(null, true, "nobody", result));
            Assert.AreEqual("player_not_found", ex.Slug);
            Assert.IsEmpty(result);
        }

        [Test]
        public void UnknownPlayerWithoutForceResolutionAddsNothing()
        {
            var result = new List<PlayerResult>();
            Assert.DoesNotThrow(() => PlayerSearch.AddResolvedProfile(null, false, "nobody", result));
            Assert.IsEmpty(result);
        }

        [Test]
        public void ResolvedProfileIsAdded()
        {
            var result = new List<PlayerResult>();
            PlayerSearch.AddResolvedProfile(new PlayerSearch.MinecraftProfile { Name = "Steve", Id = "abc" }, false, "steve", result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Steve", result[0].Name);
        }
    }
}
