using System.Collections.Generic;
using EndfieldZero.AI;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// Main pawn script — attach to pawn.tscn root (CharacterBody3D).
/// Owns data (PawnData Resource), needs, mood, AI, and animation controller.
/// Subscribes to the tick system for need decay and mood updates.
/// AI drives autonomous behavior (wander, satisfy needs, do jobs).
/// Player commands (via SelectionManager) override AI temporarily.
/// </summary>
public partial class Pawn : CharacterBody3D
{
    [Export] public PawnData Data { get; set; }
    [Export] public float BaseMoveSpeed { get; set; } = Settings.PawnBaseMoveSpeed;

    // --- Runtime state ---
    public Needs Needs { get; private set; }
    public MoodTracker Mood { get; private set; }
    public PawnAI AI { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool IsSelected { get; set; }
    public bool IsMoving => _hasTarget || _pathIndex < _path.Count;

    /// <summary>When true, AI is paused (player issued a direct command).</summary>
    public bool IsPlayerControlled { get; set; }

    private PawnAnimController _animController;
    private AnimatedSprite3D _sprite;
    private AnimationPlayer _animPlayer;

    // Direct movement
    private Vector3 _moveTarget;
    private bool _hasTarget;

    // Path following
    private List<Vector3> _path = new();
    private int _pathIndex;
    private static float PathNodeReachDist => 0.5f * Settings.BlockPixelSize;

    // Player command timer — AI resumes after idle for a while
    private long _playerCommandEndTick;

    public override void _Ready()
    {
        Data ??= new PawnData();

        Needs = new Needs();
        Data.ApplyTraitNeedModifiers(Needs);
        Mood = new MoodTracker(Data);

        _sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _animController = new PawnAnimController(_sprite, _animPlayer);

        // Initialize AI
        AI = new PawnAI(this);

        EventBus.Tick += OnTick;
        GD.Print($"[Pawn] {Data.PawnName} (ID:{Data.Id}) spawned at {GlobalPosition}");
    }

    public override void _ExitTree()
    {
        EventBus.Tick -= OnTick;
        AI?.Dispose();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive) return;

        float speed = BaseMoveSpeed * Data.GetMoveSpeedMultiplier();
        Vector3 velocity = Vector3.Zero;

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
                    _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
                    Velocity = Vector3.Zero;
                    return;
                }
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

        // Resume AI after player command timeout
        if (IsPlayerControlled && !IsMoving && tick > _playerCommandEndTick)
        {
            IsPlayerControlled = false;
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

    /// <summary>
    /// Player-issued move command. Pauses AI until arrival + timeout.
    /// </summary>
    public void PlayerMoveTo(Vector3 target)
    {
        IsPlayerControlled = true;
        _playerCommandEndTick = (TimeManager.Instance?.CurrentTick ?? 0) + 300; // 5 sec grace
        MoveTo(target);
    }

    /// <summary>
    /// Player-issued path follow. Pauses AI until arrival + timeout.
    /// </summary>
    public void PlayerFollowPath(List<Vector3> worldPath)
    {
        IsPlayerControlled = true;
        _playerCommandEndTick = (TimeManager.Instance?.CurrentTick ?? 0) + 300;
        FollowPath(worldPath);
    }

    public void Stop()
    {
        _hasTarget = false;
        _path.Clear();
        _pathIndex = 0;
        Velocity = Vector3.Zero;
    }

    public void Die(string cause)
    {
        if (!IsAlive) return;
        IsAlive = false;
        Stop();
        AI?.Dispose();
        GD.Print($"[Pawn] {Data.PawnName} died: {cause}");
        EventBus.FirePawnDied(Data.Id);
    }
}
