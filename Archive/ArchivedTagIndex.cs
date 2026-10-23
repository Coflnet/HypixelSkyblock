using System;
using System.Collections.Generic;
using MessagePack;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>One verified month of a tag in <see cref="ArchivedTagIndex"/>.</summary>
    [MessagePackObject]
    public class ArchivedMonthEntry
    {
        /// <summary>Month, <c>yyyyMM</c>.</summary>
        [Key(0)] public int Month { get; set; }
        /// <summary>Number of parts (blobs) of the month.</summary>
        [Key(1)] public int Parts { get; set; }
        /// <summary>Total rows over all parts.</summary>
        [Key(2)] public int Rows { get; set; }
        /// <summary>Sold rows over all parts.</summary>
        [Key(3)] public int SoldRows { get; set; }
        /// <summary>SHA-256 of each part (<see cref="ArchivedAuctionBlock.Sha256"/>), in part order.</summary>
        [Key(4)] public List<string> PartSha256 { get; set; } = new();
        /// <summary>Source of the blocks (mariadb|scylla|merged).</summary>
        [Key(5)] public string Source { get; set; }
        /// <summary>UTC time the read-back verification passed.</summary>
        [Key(6)] public DateTime VerifiedAt { get; set; }
    }

    /// <summary>
    /// Per-tag month index stored at <see cref="AuctionBlobKeys.Index"/>: which months of a tag are archived and verified.
    /// </summary>
    [MessagePackObject]
    public class ArchivedTagIndex
    {
        /// <summary>Item tag.</summary>
        [Key(0)] public string Tag { get; set; }
        /// <summary>Month entries, sorted ascending by month.</summary>
        [Key(1)] public List<ArchivedMonthEntry> Months { get; set; } = new();

        /// <summary>
        /// ETag of the stored object this index was read from (set by <see cref="IAuctionBlobStore.ReadIndexAsync"/>), null for a new
        /// index. Not serialised. <see cref="IAuctionBlobStore.WriteIndexAsync"/> only replaces the object if it still has this ETag
        /// (or, when null, only creates it if it does not exist) and throws <see cref="ArchiveIndexConflictException"/> otherwise;
        /// after a successful write it holds the new ETag.
        /// </summary>
        [IgnoreMember] public string ETag { get; set; }

        /// <summary>Insert the entry or replace the existing entry of the same month; keeps <see cref="Months"/> sorted.</summary>
        public ArchivedTagIndex Upsert(ArchivedMonthEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            Months ??= new List<ArchivedMonthEntry>();
            Months.RemoveAll(m => m.Month == entry.Month);
            var index = Months.FindIndex(m => m.Month > entry.Month);
            if (index < 0)
                Months.Add(entry);
            else
                Months.Insert(index, entry);
            return this;
        }

        /// <summary>The entry of a month or null.</summary>
        public ArchivedMonthEntry Find(int month) => Months?.Find(m => m.Month == month);
    }
}
