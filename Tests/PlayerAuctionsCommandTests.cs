using System;
using System.Linq;
using hypixel;
using NUnit.Framework;

namespace Tests
{
    public class PlayerAuctionsCommandTests
    {
        [Test]
        public void Simple()
        {
            var command = new PlayerAuctionsCommand();
            command.GetAllElements("384a029294fc445e863f2c42fe9709cb",0,5);
        }


    }
}