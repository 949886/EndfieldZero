using Godot;

namespace EndfieldZero.World;

public enum BlockVisualKind
{
    FlatTop,
    SolidColumn,
    Water,
    Cross,
}

/// <summary>
/// Immutable definition for a block type. Loaded once, referenced by Block.TypeId.
/// </summary>
public sealed class BlockDef
{
    public ushort Id { get; }
    public string Name { get; }
    public Color Color { get; }          // Placeholder rendering color
    public bool IsSolid { get; }         // Blocks movement?
    public bool IsTransparent { get; }   // Can see through?
    public float MoveSpeedMod { get; }   // 1.0 = normal, 0.7 = slow (mud), 0.0 = impassable
    public BlockVisualKind VisualKind { get; }
    public float BaseThickness { get; }
    public int VisualHeightLayers { get; }

    public BlockDef(
        ushort id,
        string name,
        Color color,
        bool isSolid = true,
        bool isTransparent = false,
        float moveSpeedMod = 1f,
        BlockVisualKind visualKind = BlockVisualKind.FlatTop,
        float baseThickness = 0.2f,
        int visualHeightLayers = 0)
    {
        Id = id;
        Name = name;
        Color = color;
        IsSolid = isSolid;
        IsTransparent = isTransparent;
        MoveSpeedMod = moveSpeedMod;
        VisualKind = visualKind;
        BaseThickness = Mathf.Max(baseThickness, 0.02f);
        VisualHeightLayers = Mathf.Max(visualHeightLayers, 0);
    }

    public float GetSurfaceHeight(float blockSize)
    {
        return VisualKind switch
        {
            BlockVisualKind.SolidColumn => BaseThickness + VisualHeightLayers * blockSize,
            _ => BaseThickness,
        };
    }

    public float GetDecorationHeight(float blockSize)
    {
        if (VisualKind != BlockVisualKind.Cross)
            return GetSurfaceHeight(blockSize);

        float decorativeHeight = Mathf.Max(VisualHeightLayers, 1) * blockSize * 0.75f;
        return BaseThickness + decorativeHeight;
    }

    public bool IsOccluder(float blockSize)
    {
        return VisualKind == BlockVisualKind.SolidColumn
            && GetSurfaceHeight(blockSize) >= blockSize * 0.75f;
    }
}
