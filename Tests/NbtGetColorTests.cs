using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class NbtGetColorTests
    {
        [TestCase("255:255:255", 0xFFFFFF00L)]
        [TestCase("1:2:3", 0x01020300L)]
        [TestCase("0:0:0", 0L)]
        [TestCase("5", 0x500L)]
        public void GetColorKeepsOriginalSemantics(string input, long expected)
        {
            // int arithmetic: values above 0x7FFFFFFF wrap to negative, as before the cache was added
            Assert.That(NBT.GetColor(input), Is.EqualTo((long)(int)expected));
        }

        [Test]
        public void CacheHitReturnsSameValue()
        {
            var first = NBT.GetColor("12:34:56");
            Assert.That(NBT.GetColor("12:34:56"), Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(0x0C223800L));
        }

        [Test]
        public void InvalidInputStillThrows()
        {
            Assert.Throws<System.FormatException>(() => NBT.GetColor("FFFFFF"));
            Assert.Throws<System.FormatException>(() => NBT.GetColor("FFFFFF"));
        }
    }
}
