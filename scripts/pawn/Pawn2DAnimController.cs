using Godot;
using EndfieldZero.World;

namespace EndfieldZero.Pawn;

/// <summary>
/// Controls AnimatedSprite3D + AnimationPlayer for 2D pawn visuals.
/// </summary>
public class Pawn2DAnimController
{
    private readonly AnimatedSprite3D _sprite;
    private readonly AnimationPlayer _animPlayer;

    private string _currentAnim = "";
    private PawnAnimState _state = PawnAnimState.Idle;
    private Vector3 _lastFacing = Vector3.Forward;

    public enum PawnAnimState
    {
        Idle,
        Moving,
        Working,
        Attacking,
    }

    public Pawn2DAnimController(AnimatedSprite3D sprite, AnimationPlayer animPlayer)
    {
        _sprite = sprite;
        _animPlayer = animPlayer;
    }

    public void Update(Vector3 velocity, PawnAnimState state)
    {
        _state = state;

        if (velocity.LengthSquared() > 0.01f)
            _lastFacing = velocity.Normalized();

        string target = GetTargetAnimation();
        if (target == _currentAnim)
            return;

        _currentAnim = target;
        if (_animPlayer.HasAnimation(target))
            _animPlayer.Play(target);
    }

    private string GetTargetAnimation()
    {
        Vector2 screenMotion = GetViewRelativeMotion();
        float absX = Mathf.Abs(screenMotion.X);
        float absY = Mathf.Abs(screenMotion.Y);

        bool facingRight = absX > absY;
        bool facingUp = screenMotion.Y < 0f;

        string prefix = _state switch
        {
            PawnAnimState.Moving => "move",
            PawnAnimState.Working => "dig",
            PawnAnimState.Attacking => "attack",
            _ => "idle",
        };

        if (facingRight)
        {
            _sprite.FlipH = screenMotion.X < 0f;
            return prefix switch
            {
                "idle" => "idle_right",
                "move" => "move_right",
                "dig" => "dig",
                "attack" => "attack_right",
                _ => "idle",
            };
        }

        _sprite.FlipH = false;
        if (facingUp)
        {
            return prefix switch
            {
                "idle" => "idle_back",
                "move" => "move_up",
                "dig" => "dig_up",
                "attack" => "attack_up",
                _ => "idle",
            };
        }

        return prefix switch
        {
            "idle" => "idle",
            "move" => "move_down",
            "dig" => "dig_down",
            "attack" => "attack_down",
            _ => "idle",
        };
    }

    private Vector2 GetViewRelativeMotion()
    {
        Vector3 facing = _lastFacing;
        facing.Y = 0f;
        if (facing.LengthSquared() <= 0.0001f)
            return Vector2.Zero;

        facing = facing.Normalized();

        if (GameCamera.Instance == null)
            return new Vector2(facing.X, facing.Z);

        Vector2 screenMotion = GameCamera.Instance.GetScreenMotion(facing);
        if (screenMotion.LengthSquared() <= 0.0001f)
            return new Vector2(facing.X, facing.Z);

        return screenMotion;
    }
}
