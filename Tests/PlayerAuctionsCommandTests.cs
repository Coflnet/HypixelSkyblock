using System;
using System.Linq;
using hypixel;
using NUnit.Framework;
using hypixel.Flipper;

namespace Tests
{
/*
    public class FlipperTests
    {
        [Test]
        public void NoFlips()
        {
            Assert.AreEqual(10000, FlipperEngine.DelayTimeFor(0));
        }

        [Test]
        public void NormalFlips()
        {
            Assert.AreEqual(1000, FlipperEngine.DelayTimeFor(300));
        }

        [Test]
        public void ToManyFlips()
        {
            // 5MIN / count = delay time
            Assert.AreEqual(600, FlipperEngine.DelayTimeFor(500));
        }
    }*/

    public class RomanNumberTests
    {
        [Test]
        public void Two()
        {
            Assert.AreEqual(2, Roman.From("II"));
        }
    }
}