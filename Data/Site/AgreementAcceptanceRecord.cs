using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Coflnet.Sky.Core;

public class AgreementAcceptanceRecord
{
    public long Id { get; private set; }
    public int UserId { get; private set; }
    [MaxLength(64)]
    public string Agreement { get; private set; }
    [MaxLength(64)]
    public string Version { get; private set; }
    [Column(TypeName = "char(64)")]
    public string Hash { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }
    [MaxLength(32)]
    public string Source { get; private set; }

    private AgreementAcceptanceRecord() { }

    public AgreementAcceptanceRecord(int userId, string agreement, TermsAcceptance acceptance)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
        agreement = NormalizeAgreement(agreement);
        acceptance = acceptance.Validate();
        UserId = userId;
        Agreement = agreement;
        Version = acceptance.Version;
        Hash = acceptance.Hash;
        AcceptedAtUtc = acceptance.AcceptedAtUtc;
        Source = acceptance.Source;
    }

    public static string NormalizeAgreement(string agreement)
    {
        if (string.IsNullOrWhiteSpace(agreement) || agreement.Length > 64
            || agreement.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw new ArgumentException("The agreement identifier is invalid", nameof(agreement));
        return agreement.ToLowerInvariant();
    }
}
