using MessagePack;

namespace Coflnet.Sky.Core;

[MessagePackObject]
public class BlockPos
{
    /// <summary>
    /// X coordinate
    /// </summary>
    [Key(0)]
    public double X { get; set; }

    /// <summary>
    /// Y coordinate
    /// </summary>
    [Key(1)]
    public double Y { get; set; }

    /// <summary>
    /// Z coordinate
    /// </summary>
    [Key(2)]
    public double Z { get; set; }
}