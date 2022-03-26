using System.Collections.Generic;
using System.Text;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Adopted from https://stackoverflow.com/a/22039673
    /// </summary>
    public static class Roman
    {
        public static readonly Dictionary<char, int> RomanNumberDictionary;
        public static readonly Dictionary<int, string> NumberRomanDictionary;

        static Roman()
        {
            RomanNumberDictionary = new Dictionary<char, int>
        {
            { 'I', 1 },
            { 'V', 5 },
            { 'X', 10 }
        };
        }

        public static int From(string roman)
        {
            int total = 0;

            int current, previous = 0;
            char currentRoman, previousRoman = '\0';

            for (int i = 0; i < roman.Length; i++)
            {
                currentRoman = roman[i];

                previous = previousRoman != '\0' ? RomanNumberDictionary[previousRoman] : '\0';
                current = RomanNumberDictionary[currentRoman];

                if (previous != 0 && current > previous)
                {
                    total = total - (2 * previous) + current;
                }
                else
                {
                    total += current;
                }

                previousRoman = currentRoman;
            }

            return total;
        }
    }
}
