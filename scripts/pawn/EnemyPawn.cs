using System.Collections.Generic;
using EndfieldZero.AI;
using EndfieldZero.Combat;
using EndfieldZero.Core;
using EndfieldZero.Managers;
using EndfieldZero.Pathfinding;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// Enemy pawn — hostile AI-controlled unit.
/// Unlike colonist Pawn, it cannot be selected/controlled by the player.
/// Has its own simplified AI: seek nearest colonist → attack → flee when low HP.
///
/// Attach to enemy_pawn.tscn root (CharacterBody3D).
/// </summary>
public partial class EnemyPawn : CharacterBody3D
{
    [Export] public PawnData Data { get; set; }
    [Export] public float BaseMoveSpeed { get; set; } = GameSettings.DefaultEnemyBaseMoveSpeedBlocksPerSecondValue;

    // --- Runtime state ---
    public HealthComponent Health { get; private set; }
    public bool IsAlive { get; private set; } = true;
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
    private static float PathNodeReachDist => 0.5f * Settings.BlockPixelSize;
    private const float SurfaceClearance = 0.02f;

    // Combat state
    private Pawn _target;
    private int _attackCooldown;
    private long _lastTargetSearchTick;
    private float _detectionRange;

    // Flee state
    private bool _isFleeing;
    private Vector3 _fleeTarget;

    // Selection circle (red for enemies)
    private MeshInstance3D _hostileIndicator;

    public override void _Ready()
    {
        Data ??= new PawnData();
        Data.Faction = string.IsNullOrEmpty(Data.Faction) ? "Hostile" : Data.Faction;

        Health = new HealthComponent(Data, cause => Die(cause));

        _sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _animController = new PawnAnimController(_sprite, _animPlayer);
        SnapToSurface();
        UpdateSpritePresentation();

        BaseMoveSpeed = Settings.EnemyBaseMoveSpeed;
        _detectionRange = Settings.EnemyDetectionRange;

        // Create red hostile indicator circle
        CreateHostileIndicator();

        EventBus.Tick += OnTick;
        GD.Print($"[EnemyPawn] {Data.PawnName} ({Data.Faction}) spawned at {GlobalPosition}");
    }

    public override void _ExitTree()
    {
        EventBus.Tick -= OnTick;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive) return;

        UpdateSpritePresentation();

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

        SnapToSurface();
    }

    private void OnTick(long tick)
    {
        if (!IsAlive) return;

        // Natural healing (slow)
        Health?.TickHeal();

        // Check if dead
        if (Health != null && Health.IsDead)
        {
            Die("killed");
            return;
        }

        // Flee when low HP
        if (!_isFleeing && Health != null && Health.HpPercent < Settings.EnemyFleeHpPercent)
        {
            StartFleeing();
            return;
        }

        if (_isFleeing)
        {
            UpdateFlee();
            return;
        }

        // Attack cooldown
        if (_attackCooldown > 0)
        {
            _attackCooldown--;
            return;
        }

        // Find target periodically
        if (tick - _lastTargetSearchTick >= Settings.EnemyTargetSearchIntervalTicks || _target == null)
        {
            _lastTargetSearchTick = tick;
            _target = FindNearestColonist();
        }

        if (_target == null) return;

        // Check if target is still valid
        if (!GodotObject.IsInstanceValid(_target) || !_target.IsAlive)
        {
            _target = FindNearestColonist();
            if (_target == null) return;
        }

        // In attack range?
        if (DamageSystem.IsInRange(this, _target))
        {
            // Attack
            Stop();
            // Face target
            Vector3 toTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
            toTarget.Y = 0;
            _animController.Update(toTarget, PawnAnimController.PawnAnimState.Working);
            DamageSystem.AttackEnemy(this, _target);

            var weapon = DamageSystem.GetWeapon(Data);
            _attackCooldown = Mathf.Max(1, Mathf.RoundToInt(weapon.CooldownTicks * Settings.HostileCooldownMultiplier));

            // Check if target died
            if (_target.Health != null && _target.Health.IsDead)
            {
                _target = FindNearestColonist();
                if (_target != null)
                    NavigateToTarget();
            }
        }
        else if (!IsMoving)
        {
            // Move towards target
            NavigateToTarget();
        }
    }

    // --- Flee logic ---

    private void StartFleeing()
    {
        _isFleeing = true;
        _target = null;
        Stop();

        // Flee away from colony center
        Vector3 center = PawnManager.Instance?.GetColonyCenter() ?? Vector3.Zero;
        Vector3 awayDir = (GlobalPosition - center).Normalized();
        awayDir.Y = 0;
        if (awayDir.LengthSquared() < 0.01f) awayDir = new Vector3(1, 0, 0);

        float fleeDist = 80f * Settings.BlockPixelSize;
        _fleeTarget = GlobalPosition + awayDir * fleeDist;
        MoveTo(_fleeTarget);
        GD.Print($"[EnemyPawn] {Data.PawnName} is fleeing!");
    }

    private void UpdateFlee()
    {
        if (!IsMoving)
        {
            // Reached edge — remove from game
            GD.Print($"[EnemyPawn] {Data.PawnName} fled the map");
            Die("fled");
        }
    }

    // --- Target finding ---

    private Pawn FindNearestColonist()
    {
        if (PawnManager.Instance == null) return null;

        Pawn best = null;
        float bestDist = float.MaxValue;

        foreach (var other in PawnManager.Instance.GetAllPawns())
        {
            if (!other.IsAlive) continue;
            if (other.Data.Faction != "Colony") continue;

            float dist = GlobalPosition.DistanceTo(other.GlobalPosition);
            if (dist < _detectionRange && dist < bestDist)
            {
                bestDist = dist;
                best = other;
            }
        }

        return best;
    }

    private void NavigateToTarget()
    {
        if (_target == null) return;

        Vector3 targetPos = _target.GlobalPosition;
        var weapon = DamageSystem.GetWeapon(Data);

        // Ranged: keep at 60% max range
        if (weapon.IsRanged)
        {
            float desiredDist = weapon.Range * Settings.HostileRangeMultiplier * Settings.BlockPixelSize
                * Settings.HostilePreferredRangedDistanceRatio;
            Vector3 dir = (GlobalPosition - targetPos).Normalized();
            targetPos = _target.GlobalPosition + dir * desiredDist;
        }

        if (PathfindingService.Instance != null)
        {
            var start = PathfindingService.WorldToBlock(GlobalPosition);
            var end = PathfindingService.WorldToBlock(targetPos);
            var path = PathfindingService.Instance.FindPath(start, end);
            var worldPath = PathfindingService.PathToWorld(path);
            if (worldPath != null && worldPath.Count > 0)
            {
                FollowPath(worldPath);
                return;
            }
        }
        MoveTo(targetPos);
    }

    // --- Movement API ---

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
        GD.Print($"[EnemyPawn] {Data.PawnName} died: {cause}");
        EventBus.FirePawnDied(Data.Id);

        // Remove from scene after short delay
        var timer = GetTree().CreateTimer(0.5);
        timer.Timeout += QueueFree;
    }

    // --- Visuals ---

    private void CreateHostileIndicator()
    {
        // Red circle under enemy to distinguish from colonists
        var mesh = new TorusMesh();
        mesh.InnerRadius = 0.3f;
        mesh.OuterRadius = 0.45f;
        mesh.Rings = 16;
        mesh.RingSegments = 16;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1f, 0.15f, 0.1f, 0.7f);
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.NoDepthTest = true;
        material.RenderPriority = -1;
        mesh.Material = material;

        _hostileIndicator = new MeshInstance3D();
        _hostileIndicator.Mesh = mesh;
        _hostileIndicator.Position = new Vector3(0, 0.05f, 0);
        // Flatten the torus to be a ring on the ground
        _hostileIndicator.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(_hostileIndicator);
    }

    private void UpdateSpritePresentation()
    {
        if (_sprite == null) return;

        bool angled3D = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;
        _sprite.NoDepthTest = angled3D;
        _sprite.RenderPriority = angled3D ? 10 : 0;
        UpdateSpriteAnchor();
    }

    private void SnapToSurface()
    {
        if (WorldManager.Instance == null) return;

        Vector2I blockCoord = PathfindingService.WorldToBlock(GlobalPosition);
        float surfaceY = WorldManager.Instance.GetSurfaceTopY(blockCoord.X, blockCoord.Y);
        GlobalPosition = new Vector3(GlobalPosition.X, surfaceY + SurfaceClearance, GlobalPosition.Z);
    }

    private void UpdateSpriteAnchor()
    {
        if (_sprite == null) return;

        Texture2D frameTexture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        if (frameTexture == null) return;

        float halfHeight = frameTexture.GetHeight() * _sprite.PixelSize * 0.5f;
        _sprite.Position = new Vector3(0f, halfHeight, 0f);
    }
}
