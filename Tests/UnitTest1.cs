using System;
using System.Linq;
using Coflnet;
using hypixel;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        [Test]
        public void ItemDetailsTest()
        {
            FileController.dataPaht = "/media/ekwav/Daten25/dev/hypixel/server/ah";
            var instance = ItemDetails.Instance.ReverseNames;
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(instance));
        }
    }
}