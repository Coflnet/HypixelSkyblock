using System;

namespace hypixel
{
    static class DateHelper
    {
        public static long ToUnix(this DateTime time)
        {
            return (long)time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        /// <summary>
        /// Converts a long (unix timestamp) to a time
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static DateTime ThisIsNowATimeStamp(this long time)
        {
            return (new DateTime(1970, 1, 1)).AddSeconds(time);
        }
    }
}
