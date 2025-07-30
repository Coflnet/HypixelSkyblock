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

    public override string ToString()
    {
        return $"BlockPos(X: {X}, Y: {Y}, Z: {Z})";
    }

    public override bool Equals(object obj)
    {
        if (obj is BlockPos other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }
        return false;
    }
}