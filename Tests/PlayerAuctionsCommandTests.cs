using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests;

public class RomanNumberTests
{
    [Test]
    public void Two()
    {
        Assert.That(2, Is.EqualTo(Roman.From("II")));
    }
}