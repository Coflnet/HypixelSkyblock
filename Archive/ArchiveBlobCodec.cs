using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>Wire format of archive blobs: MessagePack, Brotli compressed (<see cref="CompressionLevel.SmallestSize"/>).</summary>
    public static class ArchiveBlobCodec
    {
        /// <summary>Serialise and compress <paramref name="value"/> into <paramref name="destination"/> (left open).</summary>
        public static void Write<T>(Stream destination, T value, CancellationToken cancellationToken = default)
        {
            using var brotli = new BrotliStream(destination, CompressionLevel.SmallestSize, leaveOpen: true);
            MessagePackSerializer.Serialize(brotli, value, cancellationToken: cancellationToken);
        }

        /// <summary>Decompress and deserialise a blob stream.</summary>
        public static async Task<T> ReadAsync<T>(Stream compressed, CancellationToken cancellationToken = default)
        {
            using var brotli = new BrotliStream(compressed, CompressionMode.Decompress, leaveOpen: true);
            return await MessagePackSerializer.DeserializeAsync<T>(brotli, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
