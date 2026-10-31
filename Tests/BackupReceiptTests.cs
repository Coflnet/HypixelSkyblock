using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.Sky.Core.Archive;
using NUnit.Framework;

namespace Coflnet.Sky.Core.Tests;

public class BackupReceiptTests
{
    private const int Month = 202301;
    private const string Json = """
        { "version":1, "month":"2023-01", "snapshot":"auction-archive-20261101T0300Z", "createdAt":"2026-11-01T03:00:00Z",
          "tags":[ {"tag":"HYPERION","parts":2,"rows":30,"partSha256":["aa","bb"],
                    "objects":[ {"key":"auctions/v2/HYPERION/2023-01/000.msgpack.br","etag":"\"e0\"","size":10},
                                {"key":"auctions/v2/HYPERION/2023-01/001.msgpack.br","etag":"e1","size":11} ] } ] }
        """;

    private static (string, ArchivedMonthEntry) Live(int parts = 2, int rows = 30, params string[] hashes)
        => ("HYPERION", new ArchivedMonthEntry { Month = Month, Parts = parts, Rows = rows, PartSha256 = new List<string>(hashes.Length == 0 ? new[] { "AA", "bb" } : hashes) });

    private static Func<string, Task<string>> Etags(params (string key, string etag)[] map)
    {
        var d = new Dictionary<string, string>();
        foreach (var (k, e) in map)
            d[k] = e;
        return key => Task.FromResult(d.GetValueOrDefault(key));
    }

    private static readonly (string, string)[] Matching =
    {
        ("auctions/v2/HYPERION/2023-01/000.msgpack.br", "\"E0\""),
        ("auctions/v2/HYPERION/2023-01/001.msgpack.br", "\"e1\""),
    };

    [Test]
    public void ParsesTheFixedSchema()
    {
        var r = BackupReceipt.Parse(Json);
        Assert.That(r.Version, Is.EqualTo(1));
        Assert.That(r.Month, Is.EqualTo("2023-01"));
        Assert.That(r.Snapshot, Is.EqualTo("auction-archive-20261101T0300Z"));
        Assert.That(r.CreatedAt.ToUniversalTime(), Is.EqualTo(new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc)));
        Assert.That(r.Tags, Has.Count.EqualTo(1));
        Assert.That(r.Tags[0].PartSha256, Is.EqualTo(new[] { "aa", "bb" }));
        Assert.That(r.Tags[0].Objects[1].Size, Is.EqualTo(11));
        Assert.That(BackupReceipt.Key(Month), Is.EqualTo("auctions/v2/_meta/backup/2023-01.json"));
    }

    [Test]
    public void RoundTripsThroughJson()
    {
        var r = BackupReceipt.Parse(BackupReceipt.Parse(Json).ToJson());
        Assert.That(r.Tags[0].Objects[0].ETag, Is.EqualTo("\"e0\""));
    }

    [TestCase("", "empty")]
    [TestCase("{ not json", "not valid JSON")]
    [TestCase("""{"version":2,"month":"2023-01","snapshot":"a@b","tags":[]}""", "version")]
    [TestCase("""{"version":1,"month":"2023/01","snapshot":"a@b","tags":[]}""", "yyyy-MM")]
    [TestCase("""{"version":1,"month":"2023-01","snapshot":"","tags":[]}""", "snapshot")]
    [TestCase("""{"version":1,"month":"2023-01","snapshot":null,"tags":[]}""", "snapshot")]
    [TestCase("""{"version":1,"month":"2023-01","snapshot":"a@b","tags":[{"tag":"X","parts":1,"rows":1,"partSha256":[],"objects":[]}]}""", "hashes")]
    [TestCase("""{"version":1,"month":"2023-01","snapshot":"a@b","tags":[{"tag":"X","parts":0,"rows":0,"partSha256":[]},{"tag":"X","parts":0,"rows":0,"partSha256":[]}]}""", "twice")]
    [TestCase("""{"version":1,"month":"2023-01","snapshot":"a@b","tags":[{"tag":"X","parts":0,"rows":0,"partSha256":[],"objects":[{"key":"k","etag":"","size":1}]}]}""", "object")]
    public void RejectsMalformedReceipts(string json, string reason)
    {
        var ex = Assert.Throws<FormatException>(() => BackupReceipt.Parse(json));
        Assert.That(ex.Message, Does.Contain(reason));
    }

    /// <summary>Exactly what ArchiveNasVerify's Receipt model serialises (camelCase, indented, string createdAt, short snapshot name).</summary>
    [Test]
    public void ParsesWhatTheNasToolProduces()
    {
        const string nas = """
            {
              "version": 1,
              "month": "2023-01",
              "snapshot": "auction-archive-20261101T0300Z",
              "createdAt": "2026-11-01T03:00:12.3456789+00:00",
              "tags": [
                { "tag": "HYPERION", "parts": 1, "rows": 5, "partSha256": [ "aa" ],
                  "objects": [ { "key": "auctions/v2/HYPERION/2023-01/000.msgpack.br", "etag": "d41d8cd98f00b204e9800998ecf8427e", "size": 123 } ] }
              ]
            }
            """;
        var r = BackupReceipt.Parse(nas);
        Assert.That(r.Snapshot, Is.EqualTo("auction-archive-20261101T0300Z"));
        Assert.That(r.Tags[0].Rows, Is.EqualTo(5));
        Assert.That(r.Tags[0].Objects[0].ETag, Is.EqualTo("d41d8cd98f00b204e9800998ecf8427e"));
    }

    [Test]
    public void UnstampedReceiptCountsAsNoReceipt()
        => Assert.Throws<FormatException>(() => BackupReceipt.Parse("""{"version":1,"month":"2023-01","snapshot":"","createdAt":"x","tags":[]}"""));

    [Test]
    public async Task AcceptsAReceiptThatMatchesTheLiveState()
    {
        var problem = await BackupReceipt.Parse(Json).FindProblemAsync(Month, new[] { Live() }, Etags(Matching));
        Assert.That(problem, Is.Null);
    }

    [Test]
    public async Task RejectsAnETagThatChanged()
    {
        var etags = new[] { Matching[0], ("auctions/v2/HYPERION/2023-01/001.msgpack.br", "\"zzz\"") };
        var problem = await BackupReceipt.Parse(Json).FindProblemAsync(Month, new[] { Live() }, Etags(etags));
        Assert.That(problem, Does.Contain("001.msgpack.br").And.Contain("rewritten"));
    }

    [Test]
    public async Task RejectsPartsAddedSinceTheBackup()
    {
        var problem = await BackupReceipt.Parse(Json).FindProblemAsync(Month, new[] { Live(parts: 3, rows: 40, "aa", "bb", "cc") }, Etags(Matching));
        Assert.That(problem, Does.Contain("changed since the backup"));
    }

    [Test]
    public async Task RejectsADifferentHashOrRowCount()
    {
        var r = BackupReceipt.Parse(Json);
        Assert.That(await r.FindProblemAsync(Month, new[] { Live(hashes: new[] { "aa", "xx" }) }, Etags(Matching)), Does.Contain("part 1 hash"));
        Assert.That(await r.FindProblemAsync(Month, new[] { Live(rows: 31) }, Etags(Matching)), Does.Contain("changed since the backup"));
    }

    [Test]
    public async Task RejectsAMissingTagMissingObjectAndVanishedObject()
    {
        var r = BackupReceipt.Parse(Json);
        Assert.That(await r.FindProblemAsync(Month, new[] { ("TERMINATOR", new ArchivedMonthEntry { Month = Month }) }, Etags(Matching)), Does.Contain("TERMINATOR is not in the backup receipt"));
        r.Tags[0].Objects.RemoveAt(1);
        Assert.That(await r.FindProblemAsync(Month, new[] { Live() }, Etags(Matching)), Does.Contain("no object for"));
        Assert.That(await BackupReceipt.Parse(Json).FindProblemAsync(Month, new[] { Live() }, Etags(Matching[0])), Does.Contain("no longer exists"));
        Assert.That(await BackupReceipt.Parse(Json).FindProblemAsync(Month, new[] { ("HYPERION", (ArchivedMonthEntry)null) }, Etags(Matching)), Does.Contain("no R2 tag index entry"));
    }

    [Test]
    public async Task RejectsAReceiptOfAnotherMonth()
    {
        var problem = await BackupReceipt.Parse(Json).FindProblemAsync(202302, new[] { Live() }, Etags(Matching));
        Assert.That(problem, Does.Contain("not 2023-02"));
    }

    [Test]
    public void ETagComparisonIgnoresQuotesAndCase()
    {
        Assert.That(BackupReceipt.ETagEquals("\"AbC\"", "abc"), Is.True);
        Assert.That(BackupReceipt.ETagEquals("abc", "abd"), Is.False);
        Assert.That(BackupReceipt.ETagEquals(null, "abc"), Is.False);
    }
}
