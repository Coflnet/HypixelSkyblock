using System;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// A conditional write of a tag index (or a meta object such as a <see cref="ScyllaMonthManifest"/>) failed because the stored
    /// object changed since it was read (HTTP 412 / 409 PreconditionFailed): another writer updated it. Re-read, re-apply the change and retry.
    /// </summary>
    public class ArchiveIndexConflictException : Exception
    {
        /// <summary>Attempts (read, upsert, conditional write) a writer makes before it fails the tag-month without markers.</summary>
        public const int MaxWriteAttempts = 5;

        /// <summary>Tag whose index changed, or the key of the meta object.</summary>
        public string Tag { get; }

        /// <summary>Create the exception.</summary>
        public ArchiveIndexConflictException(string tag, Exception inner = null)
            : base($"Archive object of {tag} was changed by another writer", inner)
        {
            Tag = tag;
        }
    }
}
