using System;
using System.Collections.Generic;
using MessagePack;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>One tag-month the Scylla sweep (SkyAuctions) extended with Scylla-only parts, for SkyIndexer to index.</summary>
    [MessagePackObject]
    public class ScyllaTagImport
    {
        /// <summary>Item tag.</summary>
        [Key(0)] public string Tag { get; set; }
        /// <summary>First part written by the sweep (= phase-1 part count).</summary>
        [Key(1)] public int FirstNewPart { get; set; }
        /// <summary>Parts written by the sweep (FirstNewPart .. FirstNewPart + NewParts - 1).</summary>
        [Key(2)] public int NewParts { get; set; }
        /// <summary>Rows in the new parts.</summary>
        [Key(3)] public int NewRows { get; set; }
        /// <summary>Sold rows in the new parts.</summary>
        [Key(4)] public int NewSold { get; set; }
        /// <summary><see cref="ArchivedAuctionBlock.Sha256"/> of each new part, in part order.</summary>
        [Key(5)] public List<string> NewPartSha256 { get; set; } = new();
        /// <summary>UTC time the new parts were verified by uncached read-back.</summary>
        [Key(6)] public DateTime VerifiedAt { get; set; }
    }

    /// <summary>
    /// Hand-off of one month from the SkyAuctions Scylla sweep (which must not write MariaDB) to SkyIndexer, stored at
    /// <see cref="AuctionBlobKeys.ScyllaManifest"/>. SkyIndexer writes <c>auction_locations</c>, <c>auction_bidders</c> and the
    /// <c>archived_months</c> rows for every entry and, once <see cref="CompletedAt"/> is set and every entry is imported,
    /// <c>scylla_at</c> on the month sentinel. Only the sweep writes it (conditional PUTs); entries are keyed by tag.
    /// </summary>
    [MessagePackObject]
    public class ScyllaMonthManifest
    {
        /// <summary>Month, <c>yyyyMM</c>.</summary>
        [Key(0)] public int Month { get; set; }
        /// <summary>Tag-months with new parts, sorted by tag.</summary>
        [Key(1)] public List<ScyllaTagImport> Tags { get; set; } = new();
        /// <summary>UTC time the sweep finished every tag of the month (also set when no tag had new rows).</summary>
        [Key(2)] public DateTime? CompletedAt { get; set; }

        /// <summary>Insert the entry or replace the entry of the same tag (a re-run never duplicates); keeps <see cref="Tags"/> sorted.</summary>
        public ScyllaMonthManifest Upsert(ScyllaTagImport entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            Tags ??= new List<ScyllaTagImport>();
            Tags.RemoveAll(t => string.Equals(t.Tag, entry.Tag, StringComparison.Ordinal));
            var index = Tags.FindIndex(t => string.CompareOrdinal(t.Tag, entry.Tag) > 0);
            if (index < 0)
                Tags.Add(entry);
            else
                Tags.Insert(index, entry);
            return this;
        }

        /// <summary>The entry of a tag or null.</summary>
        public ScyllaTagImport Find(string tag) => Tags?.Find(t => string.Equals(t.Tag, tag, StringComparison.Ordinal));
    }
}
