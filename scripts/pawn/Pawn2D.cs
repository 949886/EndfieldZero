using Godot;
using EndfieldZero.World;

namespace EndfieldZero.Pawn;

public partial class Pawn2D : Pawn
{
    private AnimatedSprite3D _sprite;
    private AnimationPlayer _animPlayer;
    private Pawn2DAnimController _animController;

    protected override void InitializeVisuals()
    {
        _sprite = GetNodeOrNull<AnimatedSprite3D>("AnimatedSprite3D");
        _animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        _animController = (_sprite != null && _animPlayer != null)
            ? new Pawn2DAnimController(_sprite, _animPlayer)
            : null;
    }

    protected override void UpdateVisualPresentation(double delta)
    {
        if (_sprite == null)
            return;

        // Let billboard sprites participate in depth so front/back is resolved from world position.
        _sprite.NoDepthTest = false;
        _sprite.RenderPriority = 0;
        _sprite.AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass;
        UpdateSpriteAnchor();
    }

    protected override void UpdateVisualAnimation(Vector3 direction, PawnVisualAction action)
    {
        if (_animController == null)
            return;

        Pawn2DAnimController.PawnAnimState state = action switch
        {
            PawnVisualAction.Move => Pawn2DAnimController.PawnAnimState.Moving,
            PawnVisualAction.Idle => Pawn2DAnimController.PawnAnimState.Idle,
            _ => Pawn2DAnimController.PawnAnimState.Working,
        };

        _animController.Update(direction, state);
    }

    protected override Vector3 GetNameLabelWorldAnchor()
    {
        return GlobalPosition + new Vector3(0f, GetSpriteHalfHeight() * 1.8f, 0f);
    }

    private void UpdateSpriteAnchor()
    {
        if (_sprite == null)
            return;

        float halfHeight = GetSpriteHalfHeight();
        if (halfHeight <= 0f)
            return;

        _sprite.Position = new Vector3(0f, halfHeight, 0f);
    }

    private float GetSpriteHalfHeight()
    {
        if (_sprite == null)
            return 0f;

        Texture2D frameTexture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        if (frameTexture == null)
            return 0f;

        return frameTexture.GetHeight() * _sprite.PixelSize * 0.5f;
    }
}
