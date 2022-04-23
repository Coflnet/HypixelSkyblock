using System;
using System.Globalization;
using System.Linq;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Parses different number formats into correct types
    /// </summary>
    public class NumberParser
    {
        public static double Double(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
                return 0;
            var multiple = GetMultiplication(val);

            string normalized;
            if (multiple > 1)
                normalized = val.Replace(',', '.').Substring(0, val.Length - 1);
            else
                normalized = val.Replace(',', '.');

            return double.Parse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture) * multiple;

        }

        public static long Long(string val)
        {
            var res = Double(val);
            if(res > long.MaxValue)
                throw new NumberOutOfRangeException(res);
            return (long)Math.Round(res);
        }
        public static int Int(string val)
        {
            var res = Double(val);
            if(res > int.MaxValue)
                throw new NumberOutOfRangeException(res);
            return (int)Math.Round(res);
        }
        public static float Float(string val)
        {
            var res = Double(val);
            if(res > float.MaxValue)
                throw new NumberOutOfRangeException(res);
            return (float)Math.Round(res);
        }

        private static long GetMultiplication(string val)
        {
            return val.ToLower().Last() switch
            {
                't' => 1_000_000_000_000,
                'b' => 1_000_000_000,
                'm' => 1_000_000,
                'k' => 1_000,
                _ => 1
            };
        }

        public class NumberOutOfRangeException : CoflnetException
        {
            public NumberOutOfRangeException(double val) : base("number_out_of_range", $"The number {val} is to big/small for this input")
            {
            }
        }
    }
}
