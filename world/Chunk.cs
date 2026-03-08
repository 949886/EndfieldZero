using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Stores data for a single chunk (ChunkSize × ChunkSize × MaxLayers blocks).
/// Uses a flat array for cache-friendly access. Each chunk owns its block data
/// and tracks dirtiness for the renderer.
/// </summary>
public class Chunk
{
    /// <summary>Chunk coordinate in chunk space (not world/pixel space).</summary>
    public Vector2I ChunkCoord { get; }

    /// <summary>
    /// Flat block array. Index = layer * ChunkSize * ChunkSize + z * ChunkSize + x.
    /// </summary>
    public Block[] Blocks { get; }

    /// <summary>Whether this chunk's mesh needs to be rebuilt.</summary>
    public bool IsDirty { get; set; } = true;

    /// <summary>Whether the chunk data has been populated by the generator.</summary>
    public bool IsGenerated { get; set; }

    public Chunk(Vector2I chunkCoord)
    {
        ChunkCoord = chunkCoord;
        Blocks = new Block[Constants.ChunkSize * Constants.ChunkSize * Constants.MaxLayers];
    }

    // --- Indexing ---

    private static int Index(int localX, int localZ, int layer = 0)
    {
        return layer * Constants.ChunkSize * Constants.ChunkSize
             + localZ * Constants.ChunkSize
             + localX;
    }

    /// <summary>Get block at local coordinates within this chunk.</summary>
    public Block GetBlock(int localX, int localZ, int layer = 0)
    {
        if (!IsInBounds(localX, localZ, layer))
            return Block.Air;
        return Blocks[Index(localX, localZ, layer)];
    }

    /// <summary>Set block at local coordinates. Marks chunk as dirty.</summary>
    public void SetBlock(int localX, int localZ, Block block, int layer = 0)
    {
        if (!IsInBounds(localX, localZ, layer))
            return;
        Blocks[Index(localX, localZ, layer)] = block;
        IsDirty = true;
    }

    /// <summary>Check if local coordinates are within chunk bounds.</summary>
    public static bool IsInBounds(int localX, int localZ, int layer = 0)
    {
        return localX >= 0 && localX < Constants.ChunkSize
            && localZ >= 0 && localZ < Constants.ChunkSize
            && layer >= 0 && layer < Constants.MaxLayers;
    }

    // --- Coordinate helpers ---

    /// <summary>World-space origin of this chunk in block coordinates.</summary>
    public Vector2I WorldOrigin => ChunkCoord * Constants.ChunkSize;

    /// <summary>World-space origin of this chunk in pixel coordinates.</summary>
    public Vector2 PixelOrigin => new(
        ChunkCoord.X * Constants.ChunkPixelSize,
        ChunkCoord.Y * Constants.ChunkPixelSize
    );
}
