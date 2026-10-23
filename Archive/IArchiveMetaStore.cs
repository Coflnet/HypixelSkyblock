using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Small bookkeeping objects of the auction archive (Brotli + MessagePack like the blocks) under <see cref="AuctionBlobKeys.MetaPrefix"/>:
    /// the Scylla partition list cache and the per-month <see cref="ScyllaMonthManifest"/> SkyAuctions hands to SkyIndexer.
    /// Keys may be given with or without the prefix (<see cref="AuctionBlobKeys.Meta"/>). Implemented by the S3 stores of both services.
    /// </summary>
    public interface IArchiveMetaStore
    {
        /// <summary>Read a meta object; null when it does not exist.</summary>
        Task<T> ReadMetaAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>Read a meta object with its ETag; (null, null) when it does not exist.</summary>
        Task<(T Value, string ETag)> ReadMetaVersionedAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>Write (or overwrite) a meta object unconditionally.</summary>
        Task WriteMetaAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Write a meta object only if it still has <paramref name="etag"/> (If-Match), or, with a null etag, only if it does not
        /// exist (If-None-Match: *). Returns the new ETag; throws <see cref="ArchiveIndexConflictException"/> when it changed.
        /// </summary>
        Task<string> WriteMetaConditionalAsync<T>(string key, T value, string etag, CancellationToken cancellationToken = default) where T : class;

        /// <summary>Full keys of the meta objects under a prefix (e.g. <see cref="AuctionBlobKeys.ScyllaManifestPrefix"/>), sorted.</summary>
        Task<IReadOnlyList<string>> ListMetaKeysAsync(string prefix, CancellationToken cancellationToken = default);
    }
}
