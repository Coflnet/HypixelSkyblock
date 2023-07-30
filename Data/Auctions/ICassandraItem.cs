using System;
using System.Collections.Generic;

namespace Coflnet.Sky.Core;
#nullable enable
public interface ICassandraItem
{
    Guid ItemId { get; set; }
    long? Id { get; set; }
    string? ItemName { get; set; }
    string Tag { get; set; }
    string ExtraAttributesJson { get; set; }
    Dictionary<string, int>? Enchantments { get; set; }
    int? Color { get; set; }
}