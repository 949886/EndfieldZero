using System.Collections.Generic;
using EndfieldZero.AI;
using EndfieldZero.Combat;
using EndfieldZero.Core;
using EndfieldZero.Research;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Pawn;

public enum PawnVisualAction
{
    Idle,
    Move,
    Dig,
    Attack,
    Shoot,
    Watering,
}

/// <summary>
/// Shared colonist pawn logic.
/// Concrete visuals live in Pawn2D and Pawn3D.
/// </summary>
public abstract partial class Pawn : CharacterBody3D
{
    [Export] public PawnData Data { get; set; }
    [Export] public float BaseMoveSpeed { get; set; } = 3.125f;

    public Needs Needs { get; private set; }
    public MoodTracker Mood { get; private set; }
    public PawnAI AI { get; private set; }
    public HealthComponent Health { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool IsSelected { get; set; }
    public bool IsMoving => _hasTarget || _pathIndex < _path.Count;

    /// <summary>When true, AI is paused (player issued a direct command).</summary>
    public bool IsPlayerControlled { get; set; }

    /// <summary>When true, pawn only executes combat/move commands. Need decay halved.</summary>
    public bool IsDrafted { get; set; }

    /// <summary>Target pawn for attack command (player-issued).</summary>
    public EnemyPawn AttackTargetPawn { get; set; }

    private Vector3 _moveTarget;
    private bool _hasTarget;

    private List<Vector3> _path = new();
    private int _pathIndex;
    private static float PathNodeReachDist => 0.5f * Settings.BlockPixelSize;
    private const float SurfaceClearance = 0.02f;

    private long _playerCommandEndTick;

    private bool _isWorking;
    private Vector3 _workFacing;
    private PawnVisualAction _workAction = PawnVisualAction.Dig;

    private SelectionCircle _selectionCircle;
    private Label3D _nameLabel;
    private Label3D _nameShadowLabel;

    public override void _Ready()
    {
        Data ??= new PawnData();

        Needs = new Needs();
        Data.ApplyTraitNeedModifiers(Needs);
        Mood = new MoodTracker(Data);
        Health = new HealthComponent(Data, cause => Die(cause));

        InitializeVisuals();
        SnapToSurface();
        UpdateVisualPresentation(0d);

        AI = new PawnAI(this);

        _selectionCircle = new SelectionCircle();
        AddChild(_selectionCircle);

        _nameShadowLabel = PawnNameLabel3D.Create(Data.PawnName, shadow: true);
        AddChild(_nameShadowLabel);

        _nameLabel = PawnNameLabel3D.Create(Data.PawnName);
        AddChild(_nameLabel);

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
        UpdateVisualPresentation(delta);
        if (!IsAlive) return;
        _selectionCircle?.SetSelected(IsSelected);

        float speed = BaseMoveSpeed * Data.GetMoveSpeedMultiplier();
        if (Data.Faction == "Colony")
            speed *= TechnologyManager.Instance?.ColonyMoveSpeedMultiplier ?? 1f;
        Vector3 velocity = Vector3.Zero;

        if (_pathIndex < _path.Count)
        {
            Vector3 target = _path[_pathIndex];
            Vector3 direction = target - GlobalPosition;
            direction.Y = 0f;

            if (direction.Length() < PathNodeReachDist)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    UpdateVisualAnimation(Vector3.Zero, PawnVisualAction.Idle);
                    Velocity = Vector3.Zero;
                    return;
                }

                target = _path[_pathIndex];
                direction = target - GlobalPosition;
                direction.Y = 0f;
            }

            velocity = direction.Normalized() * speed;
        }
        else if (_hasTarget)
        {
            Vector3 direction = _moveTarget - GlobalPosition;
            direction.Y = 0f;

            if (direction.Length() < PathNodeReachDist)
            {
                _hasTarget = false;
                UpdateVisualAnimation(Vector3.Zero, PawnVisualAction.Idle);
                Velocity = Vector3.Zero;
                return;
            }

            velocity = direction.Normalized() * speed;
        }

        if (velocity.LengthSquared() > 0.01f)
        {
            Velocity = velocity;
            UpdateVisualAnimation(velocity, PawnVisualAction.Move);
            MoveAndSlide();
        }
        else if (_isWorking)
        {
            Velocity = Vector3.Zero;
            UpdateVisualAnimation(_workFacing, _workAction);
        }
        else
        {
            Velocity = Vector3.Zero;
            UpdateVisualAnimation(Vector3.Zero, PawnVisualAction.Idle);
        }

        SnapToSurface();
    }

    public override void _Process(double delta)
    {
        UpdateNameLabel();
    }

    protected abstract void InitializeVisuals();

    protected abstract void UpdateVisualPresentation(double delta);

    protected abstract void UpdateVisualAnimation(Vector3 direction, PawnVisualAction action);

    protected virtual Vector3 GetNameLabelWorldAnchor()
    {
        return GlobalPosition;
    }

    private void OnTick(long tick)
    {
        if (!IsAlive) return;

        if (IsDrafted)
        {
            if (tick % 2 == 0) Needs.Tick();
        }
        else
        {
            Needs.Tick();
        }

        Mood.Tick(tick);
        Health?.TickHeal();

        if (Needs.Hunger <= 0f)
        {
            Die("starvation");
            return;
        }

        if (Needs.Rest <= 0f)
        {
            Needs.Rest = 5f;
            Mood.AddThought("collapsed", "菴灘鴨荳肴髪蛟貞慍", -15f, TimeManager.TicksPerHour * 8);
        }

        if (Mood.IsBreakdownRisk() && tick % 300 == 0)
            EventBus.FirePawnMentalBreak(Data.Id);

        if (Needs.HasCritical() && tick % 60 == 0)
        {
            var (name, _) = Needs.GetMostUrgent();
            EventBus.FireNeedCritical(Data.Id, name);
        }

        if (IsPlayerControlled && !IsMoving && tick > _playerCommandEndTick)
            IsPlayerControlled = false;
    }

    public void FollowPath(List<Vector3> worldPath)
    {
        _path = worldPath ?? new List<Vector3>();
        _pathIndex = 0;
        _hasTarget = false;
    }

    public void MoveTo(Vector3 target)
    {
        _path.Clear();
        _pathIndex = 0;
        _moveTarget = new Vector3(target.X, GlobalPosition.Y, target.Z);
        _hasTarget = true;
    }

    public void PlayerMoveTo(Vector3 target)
    {
        IsPlayerControlled = true;
        _playerCommandEndTick = (TimeManager.Instance?.CurrentTick ?? 0) + 300;
        MoveTo(target);
    }

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
        _isWorking = false;
        _workAction = PawnVisualAction.Dig;
    }

    public void SetWorkTarget(Vector3 targetPos, PawnVisualAction action = PawnVisualAction.Dig)
    {
        _isWorking = true;
        _workFacing = targetPos - GlobalPosition;
        _workFacing.Y = 0f;
        if (_workFacing.LengthSquared() <= 0.0001f)
            _workFacing = Vector3.Forward;
        else
            _workFacing = _workFacing.Normalized();
        _workAction = action;
    }

    public void ClearWorkTarget()
    {
        _isWorking = false;
        _workAction = PawnVisualAction.Dig;
    }

    public void Die(string cause)
    {
        if (!IsAlive) return;

        IsAlive = false;
        Stop();
        OnDied();
        AI?.Dispose();
        GD.Print($"[Pawn] {Data.PawnName} died: {cause}");
        EventBus.FirePawnDied(Data.Id);
    }

    protected virtual void OnDied()
    {
    }

    private void UpdateNameLabel()
    {
        PawnNameLabel3D.Update(
            _nameLabel,
            _nameShadowLabel,
            GetViewport()?.GetCamera3D(),
            GetNameLabelWorldAnchor(),
            Data?.PawnName,
            IsAlive);
    }

    private void SnapToSurface()
    {
        if (WorldManager.Instance == null)
            return;

        Vector2I blockCoord = Pathfinding.PathfindingService.WorldToBlock(GlobalPosition);
        float surfaceY = WorldManager.Instance.GetSurfaceTopY(blockCoord.X, blockCoord.Y);
        GlobalPosition = new Vector3(GlobalPosition.X, surfaceY + SurfaceClearance, GlobalPosition.Z);
    }

}
