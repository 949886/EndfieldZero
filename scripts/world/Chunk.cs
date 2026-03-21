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

    /// <summary>Whether gameplay has changed this chunk since generation/load.</summary>
    public bool IsModified { get; private set; }

    public Chunk(Vector2I chunkCoord)
    {
        ChunkCoord = chunkCoord;
        Blocks = new Block[Settings.ChunkSize * Settings.ChunkSize * Settings.MaxLayers];
    }

    // --- Indexing ---

    private static int Index(int localX, int localZ, int layer = 0)
    {
        return layer * Settings.ChunkSize * Settings.ChunkSize
             + localZ * Settings.ChunkSize
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
    public void SetBlock(int localX, int localZ, Block block, int layer = 0, bool markModified = true)
    {
        if (!IsInBounds(localX, localZ, layer))
            return;

        int index = Index(localX, localZ, layer);
        if (Blocks[index].TypeId == block.TypeId
            && Blocks[index].Metadata == block.Metadata
            && Blocks[index].Layer == block.Layer)
        {
            return;
        }

        Blocks[index] = block;
        IsDirty = true;
        if (markModified)
            IsModified = true;
    }

    /// <summary>Load a full block snapshot into this chunk.</summary>
    public bool TryLoadSnapshot(Block[] snapshot, bool isModified)
    {
        if (snapshot == null || snapshot.Length != Blocks.Length)
            return false;

        System.Array.Copy(snapshot, Blocks, Blocks.Length);
        IsGenerated = true;
        IsDirty = true;
        IsModified = isModified;
        return true;
    }

    /// <summary>Create a defensive copy of the chunk's block data.</summary>
    public Block[] CreateSnapshot()
    {
        var snapshot = new Block[Blocks.Length];
        System.Array.Copy(Blocks, snapshot, Blocks.Length);
        return snapshot;
    }

    /// <summary>Check if local coordinates are within chunk bounds.</summary>
    public static bool IsInBounds(int localX, int localZ, int layer = 0)
    {
        return localX >= 0 && localX < Settings.ChunkSize
            && localZ >= 0 && localZ < Settings.ChunkSize
            && layer >= 0 && layer < Settings.MaxLayers;
    }

    // --- Coordinate helpers ---

    /// <summary>World-space origin of this chunk in block coordinates.</summary>
    public Vector2I WorldOrigin => ChunkCoord * Settings.ChunkSize;

    /// <summary>World-space origin of this chunk in 3D coordinates (XZ plane, Y=0).</summary>
    public Vector3 WorldPosition3D => new(
        ChunkCoord.X * Settings.ChunkPixelSize,
        0f,
        ChunkCoord.Y * Settings.ChunkPixelSize
    );
}
