using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Raw SQL for the permanent index tables of <see cref="ArchiveContext"/> (<c>auction_locations</c>, <c>auction_bidders</c>),
    /// shared by the MariaDB archive job (SkyIndexer) and the Scylla sweep (SkyAuctions). All values are integers formatted
    /// invariantly, so the statements can not carry injected SQL.
    /// </summary>
    public static class ArchiveIndexSql
    {
        /// <summary>Rows per INSERT statement.</summary>
        public const int BatchSize = 5000;

        /// <summary>
        /// <c>INSERT INTO auction_locations</c> in batches of <see cref="BatchSize"/>, idempotent.
        /// By default an existing row is repointed (<c>ON DUPLICATE KEY UPDATE tag_id, month, part</c>), so a re-run points it at the
        /// latest verified part. With <paramref name="keepOtherTagMonths"/> an existing row is only updated when it already belongs to
        /// the same tag-month (a re-run of that tag-month); a row of another tag or month is left as it is. The Scylla sweep uses this
        /// so a stale Scylla copy of an auction can never move the location of an auction archived elsewhere.
        /// </summary>
        public static IEnumerable<string> BuildLocationInserts(ushort tagId, ushort month, IReadOnlyList<(long uid, byte part)> locations, bool keepOtherTagMonths = false)
        {
            if (locations == null)
                throw new ArgumentNullException(nameof(locations));
            for (var offset = 0; offset < locations.Count; offset += BatchSize)
            {
                var sb = new StringBuilder("INSERT INTO auction_locations (uid, tag_id, month, part) VALUES ");
                var end = Math.Min(locations.Count, offset + BatchSize);
                for (var i = offset; i < end; i++)
                {
                    if (i > offset)
                        sb.Append(',');
                    sb.Append('(').Append(locations[i].uid.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(tagId.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(month.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(locations[i].part.ToString(CultureInfo.InvariantCulture)).Append(')');
                }
                if (keepOtherTagMonths)
                    sb.Append(" ON DUPLICATE KEY UPDATE part = IF(tag_id = VALUES(tag_id) AND month = VALUES(month), VALUES(part), part)");
                else
                    sb.Append(" ON DUPLICATE KEY UPDATE tag_id = VALUES(tag_id), month = VALUES(month), part = VALUES(part)");
                yield return sb.ToString();
            }
        }

        /// <summary><c>INSERT IGNORE INTO auction_bidders</c> in batches of <see cref="BatchSize"/> (idempotent).</summary>
        public static IEnumerable<string> BuildBidderInserts(IReadOnlyList<AuctionBidder> bidders)
        {
            if (bidders == null)
                throw new ArgumentNullException(nameof(bidders));
            for (var offset = 0; offset < bidders.Count; offset += BatchSize)
            {
                var sb = new StringBuilder("INSERT IGNORE INTO auction_bidders (bidder_id, `end`, uid, amount) VALUES ");
                var end = Math.Min(bidders.Count, offset + BatchSize);
                for (var i = offset; i < end; i++)
                {
                    if (i > offset)
                        sb.Append(',');
                    var b = bidders[i];
                    sb.Append('(').Append(b.BidderId.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(b.End.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(b.Uid.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(b.Amount.ToString(CultureInfo.InvariantCulture)).Append(')');
                }
                yield return sb.ToString();
            }
        }
    }
}
