using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Coflnet.Sky.Core;

/// <summary>
/// Players that opted out of processing. Persisted in PlayerOptOutRequests (written by the indexer only)
/// and held in memory as an immutable snapshot that is swapped atomically.
/// Readers load it with a (read only) connection string, see <see cref="PlayerOptOutRefresher"/>.
/// </summary>
public static class PlayerOptOut
{
    /// <summary>Idempotent DDL, shared by the migration and tests</summary>
    public const string CreateTableSql = "CREATE TABLE IF NOT EXISTS `PlayerOptOutRequests` (`PlayerUuid` CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, `RequestedAtUtc` DATETIME(6) NOT NULL, PRIMARY KEY (`PlayerUuid`)) ENGINE=InnoDB";

    /// <summary>Idempotent seed of <see cref="LegacyPlayerUuids"/>, shared by the migration and tests</summary>
    public const string SeedLegacySql = "INSERT IGNORE INTO `PlayerOptOutRequests` (`PlayerUuid`, `RequestedAtUtc`) VALUES ('f3c19fb53ea940f3921e90faab8e2b30', UTC_TIMESTAMP(6)), ('69100d30114a474c82bcb3bc8fd6c9ac', UTC_TIMESTAMP(6))";

    private const string SelectSql = "SELECT `PlayerUuid` FROM `PlayerOptOutRequests`";

    /// <summary>Players that were opted out before the table existed, always part of the snapshot</summary>
    public static readonly string[] LegacyPlayerUuids =
    [
        "f3c19fb53ea940f3921e90faab8e2b30",
        "69100d30114a474c82bcb3bc8fd6c9ac"
    ];

    public const string AnonymousUuidPrefix = "000000000000000000000000000000";

    private sealed record Snapshot(HashSet<string> Set, string[] Array);

    private static volatile Snapshot current = Build(LegacyPlayerUuids);

    /// <summary>Current snapshot of opted out uuids (canonical form, legacy ones first), usable in EF Contains filters</summary>
    public static string[] PlayerUuids => current.Array;

    private static Snapshot Build(IEnumerable<string> uuids)
    {
        var set = uuids.Select(Normalize).Where(u => !string.IsNullOrEmpty(u)).ToHashSet(StringComparer.Ordinal);
        foreach (var legacy in LegacyPlayerUuids)
            set.Add(legacy);
        var array = LegacyPlayerUuids.Concat(set.Except(LegacyPlayerUuids).OrderBy(u => u, StringComparer.Ordinal)).ToArray();
        return new Snapshot(set, array);
    }

    private static string Normalize(string uuid) => uuid?.Replace("-", "").ToLowerInvariant();

    public static bool IsOptedOut(string uuid)
    {
        var normalized = Normalize(uuid);
        return normalized != null && current.Set.Contains(normalized);
    }

    public static bool IsOptedOut(Guid uuid) => current.Set.Contains(uuid.ToString("N"));

    /// <summary>
    /// Connection string for loading: DBReadOnlyConnection, falling back to DBConnection.
    /// Looked up in the configuration first, then in the environment.
    /// </summary>
    public static string ResolveConnectionString(IConfiguration config)
    {
        foreach (var key in new[] { "DBReadOnlyConnection", "DBConnection" })
        {
            var value = config?[key];
            if (string.IsNullOrEmpty(value))
                value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        throw new InvalidOperationException("Neither DBReadOnlyConnection nor DBConnection is configured, can't load the player opt-out list.");
    }

    /// <summary>Loads the persisted list and swaps it in. Throws on failure, the previous snapshot stays.</summary>
    public static void Load(string connectionString)
    {
        var uuids = new List<string>();
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var command = new MySqlCommand(SelectSql, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            uuids.Add(reader.GetString(0));
        current = Build(uuids);
    }

    /// <inheritdoc cref="Load(string)"/>
    public static async Task LoadAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var uuids = new List<string>();
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(SelectSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            uuids.Add(reader.GetString(0));
        current = Build(uuids);
    }

    /// <summary>Loads the persisted list through an existing context (writer side)</summary>
    public static void Reload(HypixelContext db)
    {
        current = Build(db.Database.SqlQueryRaw<string>("SELECT `PlayerUuid` AS Value FROM `PlayerOptOutRequests`").ToList());
    }

    /// <summary>Persists an opt-out (idempotent) and refreshes the snapshot from the same connection. Writer only.</summary>
    public static void Add(HypixelContext db, string uuid)
    {
        if (!IsCanonical(uuid))
            throw new ArgumentException("Opt-out requires a canonical 32 character lowercase hex player UUID.", nameof(uuid));
        db.Database.ExecuteSqlRaw("INSERT IGNORE INTO `PlayerOptOutRequests` (`PlayerUuid`, `RequestedAtUtc`) VALUES ({0}, {1})", uuid, DateTime.UtcNow);
        Reload(db);
    }

    /// <summary>Queries the database (not the snapshot)</summary>
    public static bool Exists(HypixelContext db, string uuid) =>
        db.Database.SqlQueryRaw<string>("SELECT `PlayerUuid` AS Value FROM `PlayerOptOutRequests` WHERE `PlayerUuid`={0}", uuid).ToList().Count == 1;

    private static bool IsCanonical(string value) => value != null && value.Length == 32 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>Test hook: resets the in-memory snapshot to the legacy default.</summary>
    public static void ResetMemory() => current = Build(LegacyPlayerUuids);

    public static bool IsAnonymousUuid(string uuid)
    {
        var normalized = uuid?.Replace("-", "");
        return normalized?.Length == 32 && normalized.StartsWith(AnonymousUuidPrefix, StringComparison.Ordinal);
    }

    /// <summary>Removes everything that identifies opted out players from an auction (in place)</summary>
    public static void Mask(SaveAuction auction)
    {
        if (IsOptedOut(auction.AuctioneerId))
            Anonymize(auction);

        foreach (var bid in auction.Bids?.Where(bid => IsOptedOut(bid.Bidder)) ?? [])
            Anonymize(bid);
        if (IsOptedOut(auction.ProfileId)) auction.ProfileId = null;
        foreach (var bid in auction.Bids ?? [])
            if (IsOptedOut(bid.ProfileId)) bid.ProfileId = null;
        auction.CoopMembers?.RemoveAll(member => IsOptedOut(member.value));
        auction.ClaimedBids?.RemoveAll(member => IsOptedOut(member.value));
    }

    public static void Anonymize(SaveAuction auction)
    {
        auction.SellerId = 0;
        auction.AuctioneerId = AnonymousUuid();
        auction.ProfileId = null;
    }

    public static void Anonymize(SaveBids bid)
    {
        bid.BidderId = 0;
        bid.Bidder = AnonymousUuid();
        bid.ProfileId = null;
    }

    private static string AnonymousUuid() => AnonymousUuidPrefix + Random.Shared.Next(1, 254).ToString("X2");
}
