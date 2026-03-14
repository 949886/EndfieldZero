using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// Main pawn script — attach to pawn.tscn root (CharacterBody3D).
/// Owns data (PawnData Resource), needs, mood, and animation controller.
/// Subscribes to the tick system for need decay and mood updates.
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

    private PawnAnimController _animController;
    private AnimatedSprite3D _sprite;
    private AnimationPlayer _animPlayer;

    // Movement
    private Vector3 _moveTarget;
    private bool _hasTarget;

    public override void _Ready()
    {
        // Ensure PawnData exists
        Data ??= new PawnData();

        // Initialize sub-systems
        Needs = new Needs();
        Data.ApplyTraitNeedModifiers(Needs);

        Mood = new MoodTracker(Data);

        // Cache child nodes
        _sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _animController = new PawnAnimController(_sprite, _animPlayer);

        // Subscribe to tick
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

        float dt = (float)delta;

        if (_hasTarget)
        {
            Vector3 direction = (_moveTarget - GlobalPosition);
            direction.Y = 0; // Stay on XZ plane

            float distance = direction.Length();
            if (distance < 5f) // Close enough
            {
                _hasTarget = false;
                Velocity = Vector3.Zero;
                _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
            }
            else
            {
                float speed = BaseMoveSpeed * Data.GetMoveSpeedMultiplier();
                Velocity = direction.Normalized() * speed;
                _animController.Update(Velocity, PawnAnimController.PawnAnimState.Moving);
                MoveAndSlide();
            }
        }
        else
        {
            _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
        }
    }

    // --- Tick handler ---

    private void OnTick(long tick)
    {
        if (!IsAlive) return;

        // Decay needs
        Needs.Tick();

        // Update mood (expire thoughts)
        Mood.Tick(tick);

        // Check critical needs
        if (Needs.Hunger <= 0f)
        {
            Die("starvation");
            return;
        }

        if (Needs.Rest <= 0f)
        {
            // Collapse — force sleep (simplified for now)
            Needs.Rest = 5f;
            Mood.AddThought("collapsed", "体力不支倒地", -15f, TimeManager.TicksPerHour * 8);
        }

        // Mood-based events
        if (Mood.IsBreakdownRisk() && tick % 300 == 0) // Check every 5 seconds
        {
            EventBus.FirePawnMentalBreak(Data.Id);
        }

        // Check critical needs for events
        if (Needs.HasCritical() && tick % 60 == 0)
        {
            var (name, _) = Needs.GetMostUrgent();
            EventBus.FireNeedCritical(Data.Id, name);
        }
    }

    // --- Public API ---

    /// <summary>Set a movement target on XZ plane.</summary>
    public void MoveTo(Vector3 target)
    {
        _moveTarget = new Vector3(target.X, GlobalPosition.Y, target.Z);
        _hasTarget = true;
    }

    /// <summary>Stop all movement.</summary>
    public void Stop()
    {
        _hasTarget = false;
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
