using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// Backward-compatible wrapper for legacy sprite pawns (enemy pawns still use this).
/// Colonist 2D visuals should prefer Pawn2DAnimController directly.
/// </summary>
public class PawnAnimController : Pawn2DAnimController
{
    public PawnAnimController(AnimatedSprite3D sprite, AnimationPlayer animPlayer)
        : base(sprite, animPlayer)
    {
    }
}
