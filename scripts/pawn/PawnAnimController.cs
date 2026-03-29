using Godot;
using EndfieldZero.World;

namespace EndfieldZero.Pawn;

/// <summary>
/// Controls the AnimatedSprite3D and AnimationPlayer on a pawn based on
/// movement direction and current action.
///
/// Direction mapping:
///   Moving up (-Z)    → move_up
///   Moving down (+Z)  → move_down
///   Moving right (+X) → move_right (flip_h = false)
///   Moving left (-X)  → move_right (flip_h = true)
///
///   Idle follows the same pattern with idle_* animations.
///   Working uses dig / dig_up / dig_down based on facing direction.
/// </summary>
public class PawnAnimController
{
    private readonly AnimatedSprite3D _sprite;
    private readonly AnimationPlayer _animPlayer;

    private string _currentAnim = "";
    private PawnAnimState _state = PawnAnimState.Idle;
    private Vector3 _lastFacing = Vector3.Forward; // default facing -Z

    public enum PawnAnimState
    {
        Idle,
        Moving,
        Working,
    }

    public PawnAnimController(AnimatedSprite3D sprite, AnimationPlayer animPlayer)
    {
        _sprite = sprite;
        _animPlayer = animPlayer;
    }

    /// <summary>Update animation based on velocity. Call every frame.</summary>
    public void Update(Vector3 velocity, PawnAnimState state)
    {
        _state = state;

        // Track facing direction when moving
        if (velocity.LengthSquared() > 0.01f)
        {
            _lastFacing = velocity.Normalized();
        }

        string target = GetTargetAnimation();

        if (target != _currentAnim)
        {
            _currentAnim = target;
            PlayAnimation(target);
        }
    }

    private string GetTargetAnimation()
    {
        Vector2 screenMotion = GameCamera.Instance?.GetScreenMotion(_lastFacing) ?? new Vector2(_lastFacing.X, _lastFacing.Z);

        // Determine primary direction relative to the camera.
        float absX = Mathf.Abs(screenMotion.X);
        float absY = Mathf.Abs(screenMotion.Y);

        bool facingRight = absX > absY;
        bool facingUp = screenMotion.Y < 0f;

        string prefix = _state switch
        {
            PawnAnimState.Moving => "move",
            PawnAnimState.Working => "dig",
            _ => "idle",
        };

        // Handle flip for left direction
        if (facingRight)
        {
            _sprite.FlipH = screenMotion.X < 0f;

            return prefix switch
            {
                "idle" => "idle_right",
                "move" => "move_right",
                "dig" => "dig",          // side dig
                _ => "idle",
            };
        }
        else
        {
            _sprite.FlipH = false;

            if (facingUp)
            {
                return prefix switch
                {
                    "idle" => "idle_back",
                    "move" => "move_up",
                    "dig" => "dig_up",
                    _ => "idle",
                };
            }
            else
            {
                return prefix switch
                {
                    "idle" => "idle",        // idle = idle_front
                    "move" => "move_down",
                    "dig" => "dig_down",
                    _ => "idle",
                };
            }
        }
    }

    private void PlayAnimation(string animName)
    {
        if (_animPlayer.HasAnimation(animName))
        {
            _animPlayer.Play(animName);
        }
    }
}
