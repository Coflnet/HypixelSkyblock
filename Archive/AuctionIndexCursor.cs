using System;
using System.Globalization;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Paging cursor of the player index lookups (<c>auction_bidders</c>/<c>auction_sellers</c> are ordered newest <c>End</c> first,
    /// then <c>Uid</c> descending): the next page holds the rows strictly after this position. Text form <c>{endUnixSeconds}:{uid}</c>
    /// (uid can be negative), taken from the last row of the previous page.
    /// </summary>
    public sealed record AuctionIndexCursor(uint End, long Uid)
    {
        /// <summary>The cursor that continues after an auction.</summary>
        public static AuctionIndexCursor After(DateTime endUtc, long uid)
        {
            var seconds = new DateTimeOffset(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
            return new AuctionIndexCursor(seconds <= 0 ? 0 : seconds >= uint.MaxValue ? uint.MaxValue : (uint)seconds, uid);
        }

        /// <summary>Parse <c>{end}:{uid}</c>; false for anything else.</summary>
        public static bool TryParse(string text, out AuctionIndexCursor cursor)
        {
            cursor = null;
            var split = text?.IndexOf(':') ?? -1;
            if (split <= 0)
                return false;
            if (!uint.TryParse(text.AsSpan(0, split), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
                || !long.TryParse(text.AsSpan(split + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid))
                return false;
            cursor = new AuctionIndexCursor(end, uid);
            return true;
        }

        /// <summary>Text form, see <see cref="TryParse"/>.</summary>
        public override string ToString() => End.ToString(CultureInfo.InvariantCulture) + ":" + Uid.ToString(CultureInfo.InvariantCulture);
    }
}
