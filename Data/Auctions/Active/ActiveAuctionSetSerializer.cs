using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// A SaveAuction together with the members MessagePack ignores on it (ItemId).
    /// </summary>
    public class ActiveAuctionRecord
    {
        public SaveAuction Auction { get; set; }
        public int ItemId { get; set; }
    }

    /// <summary>
    /// Streaming (de)serialization of auctions for transferring the active set between services.
    /// Format: a sequence of chunks, each is a 4 byte little endian length followed by that many bytes
    /// of an lz4 block array compressed MessagePack <c>List&lt;ActiveAuctionRecord&gt;</c> (max 2000 records).
    /// An empty set is an empty stream. Same options as SkyIndexer AuctionsController export.
    /// </summary>
    public static class ActiveAuctionSetSerializer
    {
        public const int ChunkSize = 2000;

        public static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

        public static async Task SerializeAsync(Stream target, IReadOnlyCollection<SaveAuction> auctions, CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            foreach (var chunk in auctions.Chunk(ChunkSize))
            {
                var records = chunk.Select(a => new ActiveAuctionRecord { Auction = a, ItemId = a.ItemId }).ToList();
                var bytes = MessagePackSerializer.Serialize(records, Options, ct);
                BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, bytes.Length);
                await target.WriteAsync(lengthBuffer, ct);
                await target.WriteAsync(bytes, ct);
            }
            await target.FlushAsync(ct);
        }

        public static async IAsyncEnumerable<SaveAuction> DeserializeAsync(Stream source, [EnumeratorCancellation] CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            while (true)
            {
                var read = await ReadFullAsync(source, lengthBuffer, ct);
                if (read == 0)
                    yield break;
                if (read < 4)
                    throw new EndOfStreamException("truncated chunk header");
                var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                var data = new byte[length];
                if (await ReadFullAsync(source, data, ct) < length)
                    throw new EndOfStreamException("truncated chunk");
                var records = MessagePackSerializer.Deserialize<List<ActiveAuctionRecord>>(data, Options, ct);
                foreach (var record in records)
                {
                    record.Auction.ItemId = record.ItemId;
                    yield return record.Auction;
                }
            }
        }

        private static async Task<int> ReadFullAsync(Stream s, byte[] buffer, CancellationToken ct)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = await s.ReadAsync(buffer.AsMemory(total), ct);
                if (n == 0)
                    break;
                total += n;
            }
            return total;
        }
    }
}
