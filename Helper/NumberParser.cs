using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Parses different number formats into correct types
    /// </summary>
    public class NumberParser
    {
        public static double Double(string val)
        {
            if (TryDouble(val, out double result))
                return result;
            throw new CoflnetException("number_invalid", $"{val} is not a valid number");
        }

        public static bool TryLong(string val, out long result)
        {
            var returnVal = TryDouble(val, out double res);
            result = (long)Math.Round(res);
            return returnVal;
        }

        public static bool TryDouble(string val, out double result)
        {
            if (string.IsNullOrWhiteSpace(val))
            {
                result = 0;
                return true;
            }
            val =val.TrimEnd('%');
            var multiple = GetMultiplication(val);

            string normalized;
            if (multiple > 1)
                normalized = val.Replace(',', '.').Substring(0, val.Length - 1);
            else
                normalized = val.Replace(',', '.');

            var cleared = Regex.Replace(normalized, "[^0-9\\.]", "");
            if(double.TryParse(cleared, NumberStyles.Any, CultureInfo.InvariantCulture, out double internalRes))
            {
                result = internalRes * multiple;
                return true;
            }
            result = 0;
            return false;
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
