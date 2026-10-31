using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Coflnet.Sky.Core.Archive;

/// <summary>One R2 object of a backed up tag-month as the NAS mirror saw it.</summary>
public class BackupReceiptObject
{
    /// <summary>Full R2 key, e.g. <see cref="AuctionBlobKeys.Block"/>.</summary>
    [JsonPropertyName("key")] public string Key { get; set; }
    /// <summary>R2 ETag of the object at backup time (quotes optional).</summary>
    [JsonPropertyName("etag")] public string ETag { get; set; }
    /// <summary>Object size in bytes.</summary>
    [JsonPropertyName("size")] public long Size { get; set; }
}

/// <summary>One tag of a backed up month.</summary>
public class BackupReceiptTag
{
    /// <summary>Item tag.</summary>
    [JsonPropertyName("tag")] public string Tag { get; set; }
    /// <summary>Parts of the tag-month (= the tag index entry's <see cref="ArchivedMonthEntry.Parts"/>).</summary>
    [JsonPropertyName("parts")] public int Parts { get; set; }
    /// <summary>Rows over all parts (= <see cref="ArchivedMonthEntry.Rows"/>).</summary>
    [JsonPropertyName("rows")] public long Rows { get; set; }
    /// <summary>Part hashes in part order (= <see cref="ArchivedMonthEntry.PartSha256"/>).</summary>
    [JsonPropertyName("partSha256")] public List<string> PartSha256 { get; set; } = new();
    /// <summary>The backed up objects (at least every block of the tag-month).</summary>
    [JsonPropertyName("objects")] public List<BackupReceiptObject> Objects { get; set; } = new();
}

/// <summary>
/// Proof that a month of the auction archive is mirrored to the NAS and covered by a (held) ZFS snapshot, stored as plain JSON (not
/// Brotli/MessagePack) at <see cref="Key"/> by the NAS mirror tool and read by SkyIndexer's retention worker before it deletes the month.
/// <code>{ "version":1, "month":"yyyy-MM", "snapshot":"auction-archive-yyyymmddThhmmZ", "createdAt":ISO8601,
///   "tags":[ {"tag":..., "parts":n, "rows":n, "partSha256":[...], "objects":[{"key":..., "etag":..., "size":n}] } ] }</code>
/// </summary>
public class BackupReceipt
{
    /// <summary>The only supported schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Schema version.</summary>
    [JsonPropertyName("version")] public int Version { get; set; }
    /// <summary>Month as <c>yyyy-MM</c>.</summary>
    [JsonPropertyName("month")] public string Month { get; set; }
    /// <summary>Short name of the ZFS snapshot that contains the mirrored month, e.g. <c>auction-archive-20261101T0300Z</c>; empty on an unstamped receipt (which counts as no receipt).</summary>
    [JsonPropertyName("snapshot")] public string Snapshot { get; set; }
    /// <summary>UTC time the receipt was written.</summary>
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    /// <summary>Tags of the month.</summary>
    [JsonPropertyName("tags")] public List<BackupReceiptTag> Tags { get; set; } = new();

    /// <summary><c>auctions/v2/_meta/backup/{yyyy-MM}.json</c></summary>
    public static string Key(int month) => AuctionBlobKeys.MetaPrefix + "backup/" + ArchiveMonths.Format(month) + ".json";

    /// <summary>Compare ETags ignoring the surrounding quotes and the case.</summary>
    public static bool ETagEquals(string a, string b)
        => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
           && string.Equals(a.Trim().Trim('"'), b.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = false, WriteIndented = true };

    /// <summary>Serialise (used by the NAS tool tests and fixtures).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Parse and validate a receipt. Throws <see cref="FormatException"/> with the reason for malformed JSON, an unsupported version, a month
    /// that is not <c>yyyy-MM</c>, a missing snapshot, duplicate or unnamed tags, negative counts or objects without key/etag.
    /// </summary>
    public static BackupReceipt Parse(string json)
    {
        var receipt = Deserialize(json);
        if (receipt.Version != CurrentVersion)
            throw new FormatException($"backup receipt version {receipt.Version} is not supported (expected {CurrentVersion})");
        ValidateMonth(receipt.Month);
        if (string.IsNullOrWhiteSpace(receipt.Snapshot))
            throw new FormatException("backup receipt does not name a snapshot (unstamped receipt)");
        receipt.Tags ??= new List<BackupReceiptTag>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in receipt.Tags)
            ValidateTag(tag, seen);
        return receipt;
    }

    private static BackupReceipt Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("backup receipt is empty");
        try
        {
            return JsonSerializer.Deserialize<BackupReceipt>(json, Options) ?? throw new FormatException("backup receipt is null");
        }
        catch (JsonException ex)
        {
            throw new FormatException("backup receipt is not valid JSON: " + ex.Message, ex);
        }
    }

    private static void ValidateMonth(string month)
    {
        try
        {
            ArchiveMonths.Parse(month ?? "");
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is ArgumentOutOfRangeException)
        {
            throw new FormatException($"backup receipt month '{month}' is not yyyy-MM");
        }
    }

    private static void ValidateTag(BackupReceiptTag tag, HashSet<string> seen)
    {
        if (tag == null || string.IsNullOrEmpty(tag.Tag))
            throw new FormatException("backup receipt has a tag entry without a tag");
        if (!seen.Add(tag.Tag))
            throw new FormatException($"backup receipt lists tag {tag.Tag} twice");
        if (tag.Parts < 0 || tag.Rows < 0)
            throw new FormatException($"backup receipt tag {tag.Tag} has negative counts");
        tag.PartSha256 ??= new List<string>();
        tag.Objects ??= new List<BackupReceiptObject>();
        if (tag.PartSha256.Count != tag.Parts)
            throw new FormatException($"backup receipt tag {tag.Tag} says {tag.Parts} parts but lists {tag.PartSha256.Count} hashes");
        if (tag.Objects.Any(o => o == null || string.IsNullOrEmpty(o.Key) || string.IsNullOrEmpty(o.ETag) || o.Size < 0))
            throw new FormatException($"backup receipt tag {tag.Tag} has an object without key, etag or a valid size");
    }

    /// <summary>
    /// The reason a month must not be deleted according to this receipt, null when it covers the current archive state exactly.
    /// <paramref name="current"/> is the live tag index entry of every tag of the month (null for a tag without index entry);
    /// <paramref name="currentETag"/> HEADs the live R2 object (null when it does not exist). Every tag of the month needs a receipt
    /// entry with identical parts, rows and part hashes (so parts added or rewritten after the backup are caught), and every block
    /// <c>0..parts-1</c> needs a receipt object whose ETag equals the live one.
    /// </summary>
    public async Task<string> FindProblemAsync(int month, IEnumerable<(string Tag, ArchivedMonthEntry Entry)> current, Func<string, Task<string>> currentETag)
    {
        if (!string.Equals(Month, ArchiveMonths.Format(month), StringComparison.Ordinal))
            return $"the receipt is for month {Month}, not {ArchiveMonths.Format(month)}";
        foreach (var (tagName, entry) in current.OrderBy(c => c.Tag, StringComparer.Ordinal))
        {
            var problem = await TagProblemAsync(month, tagName, entry, currentETag).ConfigureAwait(false);
            if (problem != null)
                return problem;
        }
        return null;
    }

    private async Task<string> TagProblemAsync(int month, string tagName, ArchivedMonthEntry entry, Func<string, Task<string>> currentETag)
    {
        var tag = Tags?.Find(t => string.Equals(t.Tag, tagName, StringComparison.Ordinal));
        if (tag == null)
            return $"{tagName} is not in the backup receipt";
        if (entry == null)
            return $"{tagName} has no R2 tag index entry for the month";
        if (entry.Parts != tag.Parts || entry.Rows != tag.Rows || entry.PartSha256 == null || entry.PartSha256.Count != tag.PartSha256.Count)
            return $"{tagName} changed since the backup: index has {entry.Parts} parts / {entry.Rows} rows, receipt {tag.Parts} parts / {tag.Rows} rows";
        for (var part = 0; part < tag.Parts; part++)
        {
            var problem = await PartProblemAsync(month, tag, entry, part, currentETag).ConfigureAwait(false);
            if (problem != null)
                return problem;
        }
        return null;
    }

    private static async Task<string> PartProblemAsync(int month, BackupReceiptTag tag, ArchivedMonthEntry entry, int part, Func<string, Task<string>> currentETag)
    {
        if (!string.Equals(entry.PartSha256[part], tag.PartSha256[part], StringComparison.OrdinalIgnoreCase))
            return $"{tag.Tag} part {part} hash differs from the backup receipt";
        var key = AuctionBlobKeys.Block(tag.Tag, month, part);
        var obj = tag.Objects.Find(o => string.Equals(o.Key, key, StringComparison.Ordinal));
        if (obj == null)
            return $"the backup receipt has no object for {key}";
        var live = await currentETag(key).ConfigureAwait(false);
        if (live == null)
            return $"{key} no longer exists in R2";
        if (!ETagEquals(live, obj.ETag))
            return $"{key} has ETag {live.Trim('"')} now, the backup has {obj.ETag.Trim('"')} (rewritten since the backup)";
        return null;
    }
}
