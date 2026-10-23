using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>Optional columns of <see cref="ArchiveExport"/> on top of the fixed set.</summary>
    [Flags]
    public enum ExportColumns
    {
        /// <summary>The fixed column set (<see cref="ArchiveExport.StandardColumns"/>).</summary>
        Standard = 0,
        /// <summary>Adds <c>itemBytes</c> (base64 of the raw item bytes) as the last column.</summary>
        ItemBytes = 1,
    }

    /// <summary>
    /// CSV (RFC 4180: comma separated, CRLF, fields with comma, quote, CR or LF quoted and quotes doubled) and JSON lines export of
    /// archived auctions. Invariant culture; instants as ISO-8601 UTC with milliseconds (<c>2020-09-01T12:00:00.000Z</c>).
    /// CSV packs enchantments as <c>name:level;...</c> (known <see cref="Enchantment.EnchantmentType"/>s in archived order, then
    /// Scylla's unknown ones by name) and the flattened nbt as <c>key=value;...</c> sorted by key; JSON lines carry both as objects.
    /// </summary>
    public static class ArchiveExport
    {
        /// <summary>The fixed columns, in order.</summary>
        public static readonly IReadOnlyList<string> StandardColumns = new[]
        {
            "uuid", "uid", "tag", "itemName", "tier", "category", "bin", "sold", "start", "end", "startingBid", "highestBidAmount",
            "auctioneer", "profileId", "highestBidder", "count", "enchantments", "nbt", "coop", "bids", "source", "part",
        };

        /// <summary>Column names written for <paramref name="columns"/>.</summary>
        public static IReadOnlyList<string> Header(ExportColumns columns = ExportColumns.Standard)
        {
            if (!columns.HasFlag(ExportColumns.ItemBytes))
                return StandardColumns;
            return StandardColumns.Append("itemBytes").ToList();
        }

        /// <summary>Write the header and every row as CSV; returns the number of rows written.</summary>
        public static int WriteCsv(IEnumerable<ArchivedAuction> rows, TextWriter writer, ExportColumns columns = ExportColumns.Standard)
        {
            writer.Write(CsvLine(Header(columns)));
            var count = 0;
            foreach (var row in rows ?? Enumerable.Empty<ArchivedAuction>())
            {
                writer.Write(CsvLine(Fields(row, columns)));
                count++;
            }
            writer.Flush();
            return count;
        }

        /// <summary>Async variant of <see cref="WriteCsv"/> for streamed sources (e.g. an HTTP response body).</summary>
        public static async Task<int> WriteCsvAsync(IAsyncEnumerable<ArchivedAuction> rows, TextWriter writer, ExportColumns columns = ExportColumns.Standard, CancellationToken cancellationToken = default)
        {
            await writer.WriteAsync(CsvLine(Header(columns))).ConfigureAwait(false);
            var count = 0;
            await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteAsync(CsvLine(Fields(row, columns))).ConfigureAwait(false);
                count++;
            }
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }

        /// <summary>Write every row as one JSON object per line (<c>\n</c> terminated, same keys as the CSV header); returns the number of rows.</summary>
        public static int WriteJsonLines(IEnumerable<ArchivedAuction> rows, TextWriter writer, ExportColumns columns = ExportColumns.Standard)
        {
            var count = 0;
            foreach (var row in rows ?? Enumerable.Empty<ArchivedAuction>())
            {
                writer.Write(JsonLine(row, columns));
                count++;
            }
            writer.Flush();
            return count;
        }

        /// <summary>Async variant of <see cref="WriteJsonLines"/>.</summary>
        public static async Task<int> WriteJsonLinesAsync(IAsyncEnumerable<ArchivedAuction> rows, TextWriter writer, ExportColumns columns = ExportColumns.Standard, CancellationToken cancellationToken = default)
        {
            var count = 0;
            await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteAsync(JsonLine(row, columns)).ConfigureAwait(false);
                count++;
            }
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }

        /// <summary>The CSV field values of a row, in <see cref="Header"/> order (unquoted).</summary>
        public static IReadOnlyList<string> Fields(ArchivedAuction auction, ExportColumns columns = ExportColumns.Standard)
        {
            var r = auction.Row;
            var fields = new List<string>(StandardColumns.Count + 1)
            {
                r.Uuid,
                Invariant(r.UId),
                r.Tag,
                r.ItemName,
                r.Tier.ToString(),
                r.Category.ToString(),
                Bool(r.Bin),
                Bool(auction.Sold),
                FormatInstant(r.Start),
                FormatInstant(r.End),
                Invariant(r.StartingBid),
                Invariant(r.HighestBidAmount),
                r.AuctioneerId,
                r.ProfileId,
                auction.HighestBidder,
                Invariant(r.Count),
                string.Join(";", Enchantments(auction).Select(kv => kv.Key + ":" + Invariant(kv.Value))),
                string.Join(";", SortedNbt(auction).Select(kv => kv.Key + "=" + kv.Value)),
                Invariant(r.CoopMembers?.Count ?? 0),
                Invariant(r.Bids?.Count ?? 0),
                auction.Source,
                Invariant(auction.Part),
            };
            if (columns.HasFlag(ExportColumns.ItemBytes))
                fields.Add(r.NbtData?.data == null ? null : Convert.ToBase64String(r.NbtData.data));
            return fields;
        }

        /// <summary>Enchantments of a row: archived ones (name of the <see cref="Enchantment.EnchantmentType"/>) in order, then Scylla's unknown ones by name.</summary>
        public static IReadOnlyList<KeyValuePair<string, int>> Enchantments(ArchivedAuction auction)
        {
            var result = new List<KeyValuePair<string, int>>();
            foreach (var e in auction.Row.Enchantments ?? new List<Enchantment>())
                result.Add(new(e.Type.ToString(), e.Level));
            if (auction.Extras?.UnknownEnchantments != null)
                foreach (var kv in auction.Extras.UnknownEnchantments.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    result.Add(new(kv.Key, kv.Value));
            return result;
        }

        /// <summary><see cref="ArchivedAuction.NbtStrings"/> sorted by key (ordinal).</summary>
        public static IEnumerable<KeyValuePair<string, string>> SortedNbt(ArchivedAuction auction)
            => auction.NbtStrings.OrderBy(kv => kv.Key, StringComparer.Ordinal);

        /// <summary>ISO-8601 UTC with milliseconds; Unspecified is treated as UTC.</summary>
        public static string FormatInstant(DateTime value)
            => ArchivedAuctionQuery.AsUtc(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        /// <summary>One RFC 4180 record including the CRLF terminator.</summary>
        public static string CsvLine(IEnumerable<string> fields)
        {
            var sb = new StringBuilder();
            var first = true;
            foreach (var field in fields)
            {
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append(CsvField(field));
            }
            return sb.Append("\r\n").ToString();
        }

        /// <summary>A field quoted when it contains a comma, quote, CR or LF (quotes doubled); null is empty.</summary>
        public static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string JsonLine(ArchivedAuction auction, ExportColumns columns)
        {
            var r = auction.Row;
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (var json = new JsonTextWriter(sw) { Formatting = Formatting.None, Culture = CultureInfo.InvariantCulture })
            {
                json.WriteStartObject();
                Prop(json, "uuid", r.Uuid);
                json.WritePropertyName("uid"); json.WriteValue(r.UId);
                Prop(json, "tag", r.Tag);
                Prop(json, "itemName", r.ItemName);
                Prop(json, "tier", r.Tier.ToString());
                Prop(json, "category", r.Category.ToString());
                json.WritePropertyName("bin"); json.WriteValue(r.Bin);
                json.WritePropertyName("sold"); json.WriteValue(auction.Sold);
                Prop(json, "start", FormatInstant(r.Start));
                Prop(json, "end", FormatInstant(r.End));
                json.WritePropertyName("startingBid"); json.WriteValue(r.StartingBid);
                json.WritePropertyName("highestBidAmount"); json.WriteValue(r.HighestBidAmount);
                Prop(json, "auctioneer", r.AuctioneerId);
                Prop(json, "profileId", r.ProfileId);
                Prop(json, "highestBidder", auction.HighestBidder);
                json.WritePropertyName("count"); json.WriteValue(r.Count);
                json.WritePropertyName("enchantments");
                json.WriteStartObject();
                foreach (var kv in Enchantments(auction))
                {
                    json.WritePropertyName(kv.Key);
                    json.WriteValue(kv.Value);
                }
                json.WriteEndObject();
                json.WritePropertyName("nbt");
                json.WriteStartObject();
                foreach (var kv in SortedNbt(auction))
                    Prop(json, kv.Key, kv.Value);
                json.WriteEndObject();
                json.WritePropertyName("coop"); json.WriteValue(r.CoopMembers?.Count ?? 0);
                json.WritePropertyName("bids"); json.WriteValue(r.Bids?.Count ?? 0);
                Prop(json, "source", auction.Source);
                json.WritePropertyName("part"); json.WriteValue(auction.Part);
                if (columns.HasFlag(ExportColumns.ItemBytes))
                    Prop(json, "itemBytes", r.NbtData?.data == null ? null : Convert.ToBase64String(r.NbtData.data));
                json.WriteEndObject();
            }
            return sb.Append('\n').ToString();
        }

        private static void Prop(JsonWriter json, string name, string value)
        {
            json.WritePropertyName(name);
            json.WriteValue(value);
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
