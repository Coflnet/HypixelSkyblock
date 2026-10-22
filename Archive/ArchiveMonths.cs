using System;
using System.Globalization;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Month arithmetic for the auction archive. A month is written as an int <c>yyyyMM</c> (e.g. 202610)
    /// in blobs and as months since 2019-01 (<see cref="ToIndex"/>) in the MariaDB index tables.
    /// All instants are UTC; <see cref="DateTimeKind.Unspecified"/> values (as EF loads them from MariaDB) are treated as UTC.
    /// </summary>
    public static class ArchiveMonths
    {
        /// <summary>Month 0 of the index tables.</summary>
        public const int IndexEpochMonth = 201901;

        /// <summary>Month (<c>yyyyMM</c>) that contains the given instant.</summary>
        public static int MonthOf(DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Local)
                utc = utc.ToUniversalTime();
            return utc.Year * 100 + utc.Month;
        }

        /// <summary>UTC start (inclusive) and end (exclusive) of a month.</summary>
        public static (DateTime start, DateTime end) MonthRange(int month)
        {
            Validate(month);
            var start = new DateTime(month / 100, month % 100, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1));
        }

        /// <summary>
        /// A month is closed (may be archived and is never rewritten afterwards) once its end plus
        /// <paramref name="marginDays"/> is not after <paramref name="nowUtc"/>.
        /// </summary>
        public static bool IsClosed(int month, DateTime nowUtc, int marginDays = 14)
        {
            return MonthRange(month).end.AddDays(marginDays) <= nowUtc;
        }

        /// <summary>The newest month that is closed at <paramref name="nowUtc"/>.</summary>
        public static int NewestClosed(DateTime nowUtc, int marginDays = 14)
        {
            var month = MonthOf(nowUtc);
            while (!IsClosed(month, nowUtc, marginDays))
                month = Previous(month);
            return month;
        }

        /// <summary>The month containing <c>now - windowYears</c>; months before it are outside the live window.</summary>
        public static int OldestInWindow(DateTime nowUtc, int windowYears)
        {
            return MonthOf(nowUtc.AddYears(-windowYears));
        }

        /// <summary>The month after <paramref name="month"/>.</summary>
        public static int Next(int month)
        {
            Validate(month);
            return month % 100 == 12 ? (month / 100 + 1) * 100 + 1 : month + 1;
        }

        /// <summary>The month before <paramref name="month"/>.</summary>
        public static int Previous(int month)
        {
            Validate(month);
            return month % 100 == 1 ? (month / 100 - 1) * 100 + 12 : month - 1;
        }

        /// <summary>Months since 2019-01 as stored in <c>auction_locations.month</c> / <c>archived_months.month</c>.</summary>
        public static ushort ToIndex(int month)
        {
            Validate(month);
            var index = (month / 100 - IndexEpochMonth / 100) * 12 + (month % 100 - 1);
            if (index < 0 || index > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month is before 2019-01 or too far in the future");
            return (ushort)index;
        }

        /// <summary>Inverse of <see cref="ToIndex"/>.</summary>
        public static int FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            var year = IndexEpochMonth / 100 + index / 12;
            return year * 100 + index % 12 + 1;
        }

        /// <summary><c>yyyy-MM</c> as used in blob keys.</summary>
        public static string Format(int month)
        {
            Validate(month);
            return (month / 100).ToString("D4", CultureInfo.InvariantCulture) + "-" + (month % 100).ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>Parse <c>yyyy-MM</c> (or <c>yyyyMM</c>) into <c>yyyyMM</c>.</summary>
        public static int Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException("Empty month");
            var digits = value.Trim().Replace("-", "");
            if (digits.Length != 6 || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var month))
                throw new FormatException("Month must be yyyy-MM: " + value);
            Validate(month);
            return month;
        }

        private static void Validate(int month)
        {
            var m = month % 100;
            if (m < 1 || m > 12 || month / 100 < 1 || month / 100 > 9999)
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be yyyyMM");
        }
    }
}
