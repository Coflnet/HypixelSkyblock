using System;
using System.Linq;

namespace Coflnet.Sky.Core;

public record TermsAcceptance(string Version, string Hash, DateTime AcceptedAtUtc, string Source)
{
    public TermsAcceptance Validate()
    {
        if (string.IsNullOrWhiteSpace(Version) || Version.Length > 64
            || Version.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw new ArgumentException("The agreement version is invalid", nameof(Version));
        if (Hash?.Length != 64 || Hash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("The agreement hash must be a SHA-256 hex digest", nameof(Hash));
        if (AcceptedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The acceptance timestamp must be UTC", nameof(AcceptedAtUtc));
        if (string.IsNullOrWhiteSpace(Source) || Source.Length > 32
            || Source.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw new ArgumentException("The acceptance source is invalid", nameof(Source));
        return this with { Hash = Hash.ToLowerInvariant() };
    }
}
