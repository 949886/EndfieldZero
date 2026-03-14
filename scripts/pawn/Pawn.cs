using System.Collections.Generic;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// Main pawn script — attach to pawn.tscn root (CharacterBody3D).
/// Owns data (PawnData Resource), needs, mood, and animation controller.
/// Subscribes to the tick system for need decay and mood updates.
/// Supports both direct MoveTo and A* path following.
/// </summary>
public partial class Pawn : CharacterBody3D
{
    /// <summary>Pawn data resource (identity, stats, traits).</summary>
    [Export] public PawnData Data { get; set; }

    /// <summary>Movement speed in units/second (base, before Agility modifier).</summary>
    [Export] public float BaseMoveSpeed { get; set; } = 100f;

    // --- Runtime state ---
    public Needs Needs { get; private set; }
    public MoodTracker Mood { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool IsSelected { get; set; }        // Set by SelectionManager
    public bool IsMoving => _hasTarget || _pathIndex < _path.Count;

    private PawnAnimController _animController;
    private AnimatedSprite3D _sprite;
    private AnimationPlayer _animPlayer;

    // Direct movement
    private Vector3 _moveTarget;
    private bool _hasTarget;

    // Path following
    private List<Vector3> _path = new();
    private int _pathIndex;
    private const float PathNodeReachDist = 16f;   // Distance to consider a waypoint reached

    public override void _Ready()
    {
        Data ??= new PawnData();

        Needs = new Needs();
        Data.ApplyTraitNeedModifiers(Needs);
        Mood = new MoodTracker(Data);

        _sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _animController = new PawnAnimController(_sprite, _animPlayer);

        EventBus.Tick += OnTick;
        GD.Print($"[Pawn] {Data.PawnName} (ID:{Data.Id}) spawned at {GlobalPosition}");
    }

    public override void _ExitTree()
    {
        EventBus.Tick -= OnTick;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive) return;

        float speed = BaseMoveSpeed * Data.GetMoveSpeedMultiplier();
        Vector3 velocity = Vector3.Zero;

        // Path following takes priority
        if (_pathIndex < _path.Count)
        {
            Vector3 target = _path[_pathIndex];
            Vector3 direction = target - GlobalPosition;
            direction.Y = 0;

            if (direction.Length() < PathNodeReachDist)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    // Path complete
                    _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
                    Velocity = Vector3.Zero;
                    return;
                }
                // Continue to next waypoint
                target = _path[_pathIndex];
                direction = target - GlobalPosition;
                direction.Y = 0;
            }

            velocity = direction.Normalized() * speed;
        }
        else if (_hasTarget)
        {
            Vector3 direction = _moveTarget - GlobalPosition;
            direction.Y = 0;

            if (direction.Length() < PathNodeReachDist)
            {
                _hasTarget = false;
                _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
                Velocity = Vector3.Zero;
                return;
            }

            velocity = direction.Normalized() * speed;
        }

        if (velocity.LengthSquared() > 0.01f)
        {
            Velocity = velocity;
            _animController.Update(velocity, PawnAnimController.PawnAnimState.Moving);
            MoveAndSlide();
        }
        else
        {
            Velocity = Vector3.Zero;
            _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
        }
    }

    // --- Tick handler ---

    private void OnTick(long tick)
    {
        if (!IsAlive) return;

        Needs.Tick();
        Mood.Tick(tick);

        if (Needs.Hunger <= 0f)
        {
            Die("starvation");
            return;
        }

        if (Needs.Rest <= 0f)
        {
            Needs.Rest = 5f;
            Mood.AddThought("collapsed", "体力不支倒地", -15f, TimeManager.TicksPerHour * 8);
        }

        if (Mood.IsBreakdownRisk() && tick % 300 == 0)
            EventBus.FirePawnMentalBreak(Data.Id);

        if (Needs.HasCritical() && tick % 60 == 0)
        {
            var (name, _) = Needs.GetMostUrgent();
            EventBus.FireNeedCritical(Data.Id, name);
        }
    }

    // --- Public API ---

    /// <summary>Follow an A* path (list of world-space points).</summary>
    public void FollowPath(List<Vector3> worldPath)
    {
        _path = worldPath ?? new List<Vector3>();
        _pathIndex = 0;
        _hasTarget = false;
    }

    /// <summary>Set a direct movement target on XZ plane.</summary>
    public void MoveTo(Vector3 target)
    {
        _path.Clear();
        _pathIndex = 0;
        _moveTarget = new Vector3(target.X, GlobalPosition.Y, target.Z);
        _hasTarget = true;
    }

    /// <summary>Stop all movement.</summary>
    public void Stop()
    {
        _hasTarget = false;
        _path.Clear();
        _pathIndex = 0;
        Velocity = Vector3.Zero;
    }

    /// <summary>Kill this pawn.</summary>
    public void Die(string cause)
    {
        if (!IsAlive) return;
        IsAlive = false;
        Stop();
        GD.Print($"[Pawn] {Data.PawnName} died: {cause}");
        EventBus.FirePawnDied(Data.Id);
    }
}
