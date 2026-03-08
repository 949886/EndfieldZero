using Godot;

namespace EndfieldZero.World;

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

    public BlockDef(ushort id, string name, Color color, bool isSolid = true,
                    bool isTransparent = false, float moveSpeedMod = 1f)
    {
        Id = id;
        Name = name;
        Color = color;
        IsSolid = isSolid;
        IsTransparent = isTransparent;
        MoveSpeedMod = moveSpeedMod;
    }
}
