using System;
using System.Threading;
using System.Threading.Tasks;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Object storage for archived auction blocks and per-tag indexes (key layout in <see cref="AuctionBlobKeys"/>).
    /// Implementations must be thread-safe.
    /// </summary>
    public interface IAuctionBlobStore
    {
        /// <summary>True if the store is configured and may be used.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Read one part of a tag-month. Null when it does not exist; other errors propagate.
        /// <paramref name="uncached"/> always fetches from storage (and refreshes the cache), used to verify what was persisted.
        /// </summary>
        Task<ArchivedAuctionBlock> ReadBlockAsync(string tag, int month, int part, bool uncached = false, CancellationToken cancellationToken = default);

        /// <summary>Write (or overwrite) a block at its key.</summary>
        Task WriteBlockAsync(ArchivedAuctionBlock block, CancellationToken cancellationToken = default);

        /// <summary>True if the part exists in storage.</summary>
        Task<bool> BlockExistsAsync(string tag, int month, int part, CancellationToken cancellationToken = default);

        /// <summary>Read the month index of a tag (never cached); null when it does not exist.</summary>
        Task<ArchivedTagIndex> ReadIndexAsync(string tag, CancellationToken cancellationToken = default);

        /// <summary>Write (or overwrite) the month index of a tag.</summary>
        Task WriteIndexAsync(ArchivedTagIndex index, CancellationToken cancellationToken = default);
    }

    /// <summary>Object keys of the auction archive v2.</summary>
    public static class AuctionBlobKeys
    {
        /// <summary>Common prefix of all v2 keys.</summary>
        public const string Prefix = "auctions/v2/";
        /// <summary>Content type of blocks and indexes (Brotli compressed MessagePack).</summary>
        public const string ContentType = "application/x-msgpack+br";

        /// <summary><c>auctions/v2/{tag}/{yyyy-MM}/{part:D3}.msgpack.br</c></summary>
        public static string Block(string tag, int month, int part)
        {
            if (part < 0 || part > 999)
                throw new ArgumentOutOfRangeException(nameof(part), part, "Part must be 0-999");
            return TagPrefix(tag) + ArchiveMonths.Format(month) + "/" + part.ToString("D3") + ".msgpack.br";
        }

        /// <summary><c>auctions/v2/{tag}/index.msgpack.br</c></summary>
        public static string Index(string tag) => TagPrefix(tag) + "index.msgpack.br";

        /// <summary><c>auctions/v2/{tag}/</c>; the tag is URI-escaped so characters such as '/' can not change the layout (A-Z, 0-9, '_' and '-' stay as they are).</summary>
        public static string TagPrefix(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentException("Tag is required", nameof(tag));
            return Prefix + Uri.EscapeDataString(tag) + "/";
        }
    }
}
