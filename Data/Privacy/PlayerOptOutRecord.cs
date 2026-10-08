using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coflnet.Sky.Core;

/// <summary>
/// A player that opted out of processing. Written by the indexer only, read by all services through <see cref="PlayerOptOut"/>.
/// </summary>
public class PlayerOptOutRecord
{
    /// <summary>Canonical 32 character lowercase hex uuid</summary>
    [Key]
    [Column(TypeName = "char(32)")]
    public string PlayerUuid { get; set; }
    public DateTime RequestedAtUtc { get; set; }
}
