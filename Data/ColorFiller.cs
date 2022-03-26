using System;
using System.Collections.Generic;
using System.Linq;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Fills in colors for leather items
    /// </summary>
    public class ColorFiller
    {
        static Dictionary<string, El> Colors = new Dictionary<string, El>();
        public static void Add(string tag, string color)
        {
            if (Colors.TryGetValue(tag, out El value))
            {
                if (value.color == color && value.occured >= 3)
                {
                    using (var context = new HypixelContext())
                    {
                        var item = context.Items.Where(i => i.Tag == tag && i.color == null).FirstOrDefault();
                        if (item == null)
                            return;
                        item.color = color;
                        context.SaveChanges();
                        Console.WriteLine("Added color to " + tag);
                        value.occured = -500000;
                        return;
                    }
                }

            }
            if (value == null)
                value = new El();
            value.occured += 1;
            value.color = color;
            Colors[tag] = value;
        }

        class El
        {
            public int occured;
            public string color;
        }
    }
}