using Godot;

namespace EndfieldZero.Building;

/// <summary>
/// A blueprint instance — represents a planned building waiting to be constructed.
///
/// Lifecycle: Placed → Queued → Building → Complete
///   - Placed: ghost overlay visible, no job yet
///   - Queued: job created, waiting for a pawn
///   - Building: pawn is actively constructing
///   - Complete: building placed in world
/// </summary>
public class Blueprint
{
    private static int _nextId = 1;

    public int Id { get; }
    public BuildingDef Def { get; }

    /// <summary>Top-left block coordinate of this blueprint.</summary>
    public Vector2I BlockCoord { get; }

    /// <summary>Rotation: 0, 1, 2, 3 (×90°). Affects multi-tile buildings.</summary>
    public int Rotation { get; set; }

    /// <summary>Current status.</summary>
    public BlueprintStatus Status { get; set; } = BlueprintStatus.Queued;

    /// <summary>Linked job ID in JobSystem (-1 = none).</summary>
    public int LinkedJobId { get; set; } = -1;

    /// <summary>Effective size after rotation.</summary>
    public Vector2I EffectiveSize
    {
        get
        {
            if (Rotation == 1 || Rotation == 3)
                return new Vector2I(Def.Size.Y, Def.Size.X);
            return Def.Size;
        }
    }

    public Blueprint(BuildingDef def, Vector2I blockCoord, int rotation = 0)
    {
        Id = _nextId++;
        Def = def;
        BlockCoord = blockCoord;
        Rotation = rotation % 4;
    }

    /// <summary>World position (center of the blueprint area).</summary>
    public Vector3 WorldCenter
    {
        get
        {
            var size = EffectiveSize;
            float px = Core.Settings.BlockPixelSize;
            return new Vector3(
                (BlockCoord.X + size.X * 0.5f) * px,
                0f,
                (BlockCoord.Y + size.Y * 0.5f) * px
            );
        }
    }

    /// <summary>Iterate all block coordinates this blueprint occupies.</summary>
    public System.Collections.Generic.IEnumerable<Vector2I> OccupiedCells()
    {
        var size = EffectiveSize;
        for (int dz = 0; dz < size.Y; dz++)
            for (int dx = 0; dx < size.X; dx++)
                yield return new Vector2I(BlockCoord.X + dx, BlockCoord.Y + dz);
    }
}

public enum BlueprintStatus
{
    Queued,     // Job created, waiting for pawn
    Building,   // Pawn is constructing
    Complete,   // Done
}
