using System.Runtime.InteropServices;

namespace EndfieldZero.World;

/// <summary>
/// Lightweight value type representing a single block in the world.
/// Stored in flat arrays for cache-friendly access. Only 4 bytes per block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Block
{
    /// <summary>Block type ID. 0 = Air (empty).</summary>
    public ushort TypeId;

    /// <summary>Extra metadata (orientation, state variant, etc.).</summary>
    public byte Metadata;

    /// <summary>Layer index for multi-layer support (0 = ground level).</summary>
    public byte Layer;

    public Block(ushort typeId, byte metadata = 0, byte layer = 0)
    {
        TypeId = typeId;
        Metadata = metadata;
        Layer = layer;
    }

    public readonly bool IsAir => TypeId == 0;

    public static readonly Block Air = new(0);
}
