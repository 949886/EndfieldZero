using System.Collections.Generic;
using EndfieldZero.Combat;
using EndfieldZero.Core;
using EndfieldZero.Managers;
using EndfieldZero.Pathfinding;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// Enemy pawn - hostile AI-controlled unit.
/// Unlike colonist Pawn, it cannot be selected/controlled by the player.
/// Has its own simplified AI: seek nearest colonist -> attack -> flee when low HP.
///
/// Attach to enemy_pawn.tscn root (CharacterBody3D).
/// </summary>
public partial class EnemyPawn : CharacterBody3D
{
    private const float DefaultAttackAnimDurationSeconds = 0.8f;

    [Export] public PawnData Data { get; set; }
    [Export] public float BaseMoveSpeed { get; set; } = GameSettings.DefaultEnemyBaseMoveSpeedBlocksPerSecondValue;
    public HostileWaveContext WaveContext { get; set; }

    // --- Runtime state ---
    public HealthComponent Health { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool IsMoving => _hasTarget || _pathIndex < _path.Count;
    public EnemyAssaultPhase CurrentAssaultPhase { get; private set; } = EnemyAssaultPhase.Assaulting;
    public int CurrentWaveId => WaveContext?.WaveId ?? 0;

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
    private float _attackAnimTimeRemaining;
    private Vector3 _attackFacing = Vector3.Forward;
    private long _lastTargetSearchTick;
    private float _detectionRange;
    private long _prepareNextRepickTick;
    private readonly RandomNumberGenerator _rng = new();

    // Flee state
    private bool _isFleeing;
    private Vector3 _fleeTarget;

    // Selection circle (red for enemies)
    private MeshInstance3D _hostileIndicator;
    private Label3D _nameLabel;
    private Label3D _nameShadowLabel;

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
        _rng.Randomize();

        CreateHostileIndicator();
        _nameShadowLabel = PawnNameLabel3D.Create(Data.PawnName, shadow: true);
        AddChild(_nameShadowLabel);

        _nameLabel = PawnNameLabel3D.Create(Data.PawnName);
        AddChild(_nameLabel);
        InitializeAssaultState();

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
        else if (_attackAnimTimeRemaining > 0f)
        {
            Velocity = Vector3.Zero;
            _animController.Update(_attackFacing, PawnAnimController.PawnAnimState.Attacking);
            _attackAnimTimeRemaining = Mathf.Max(0f, _attackAnimTimeRemaining - (float)delta);
        }
        else
        {
            Velocity = Vector3.Zero;
            _animController.Update(Vector3.Zero, PawnAnimController.PawnAnimState.Idle);
        }

        SnapToSurface();
    }

    public override void _Process(double delta)
    {
        UpdateNameLabel();
    }

    private void OnTick(long tick)
    {
        if (!IsAlive) return;

        Health?.TickHeal();

        if (Health != null && Health.IsDead)
        {
            Die("killed");
            return;
        }

        if (!_isFleeing && Health != null && Health.HpPercent < Settings.EnemyFleeHpPercent)
        {
            StartFleeing();
            return;
        }

        if (CurrentAssaultPhase == EnemyAssaultPhase.Preparing)
        {
            if (UpdatePreparing(tick))
                return;
        }

        if (_isFleeing)
        {
            UpdateFlee();
            return;
        }

        if (_attackCooldown > 0)
        {
            _attackCooldown--;
            return;
        }

        if (tick - _lastTargetSearchTick >= Settings.EnemyTargetSearchIntervalTicks || !IsTargetValid(_target))
        {
            _lastTargetSearchTick = tick;
            _target = FindNearestColonist();
        }

        if (_target == null)
        {
            if (CurrentAssaultPhase == EnemyAssaultPhase.Assaulting && !IsMoving)
                NavigateToAssaultObjective();
            return;
        }

        if (DamageSystem.IsInRange(this, _target))
        {
            Stop();
            Vector3 toTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
            toTarget.Y = 0;
            StartAttackAnimation(toTarget);
            DamageSystem.AttackEnemy(this, _target);

            var weapon = DamageSystem.GetWeapon(Data);
            _attackCooldown = Mathf.Max(1, Mathf.RoundToInt(weapon.CooldownTicks * Settings.HostileCooldownMultiplier));

            if (_target.Health != null && _target.Health.IsDead)
            {
                _target = FindNearestColonist();
                if (_target != null)
                    NavigateToTarget();
            }
        }
        else if (!IsMoving)
        {
            NavigateToTarget();
        }
    }

    // --- Flee logic ---

    private void StartFleeing()
    {
        _isFleeing = true;
        SetAssaultPhase(EnemyAssaultPhase.Fleeing);
        _target = null;
        Stop();

        Vector3 center = PawnManager.Instance?.GetColonyCenter() ?? Vector3.Zero;
        Vector3 awayDir = (GlobalPosition - center).Normalized();
        awayDir.Y = 0;
        if (awayDir.LengthSquared() < 0.01f) awayDir = new Vector3(1, 0, 0);

        float fleeDist = 80f * Settings.BlockPixelSize;
        _fleeTarget = GlobalPosition + awayDir * fleeDist;
        MoveTo(_fleeTarget);
        GD.Print($"[EnemyPawn] {Data.PawnName} is fleeing!");
        RefreshThreatLevel();
    }

    private void UpdateFlee()
    {
        if (!IsMoving)
        {
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

    private bool IsTargetValid(Pawn target)
    {
        return target != null
            && GodotObject.IsInstanceValid(target)
            && target.IsAlive
            && target.Data.Faction == "Colony";
    }

    private void NavigateToTarget()
    {
        if (_target == null) return;

        Vector3 targetPos = _target.GlobalPosition;
        var weapon = DamageSystem.GetWeapon(Data);

        if (weapon.IsRanged)
        {
            float desiredDist = weapon.Range * Settings.HostileRangeMultiplier * Settings.BlockPixelSize
                * Settings.HostilePreferredRangedDistanceRatio;
            Vector3 dir = (GlobalPosition - targetPos).Normalized();
            targetPos = _target.GlobalPosition + dir * desiredDist;
        }

        NavigateToPosition(targetPos);
    }

    private bool UpdatePreparing(long tick)
    {
        if (WaveContext == null)
        {
            EnterAssaulting();
            return false;
        }

        if (tick >= WaveContext.PrepareUntilTick)
        {
            EnterAssaulting();
            return false;
        }

        if (!IsMoving || tick >= _prepareNextRepickTick)
            PickPrepareDestination(tick);

        return true;
    }

    private void PickPrepareDestination(long tick)
    {
        if (WaveContext == null)
        {
            EnterAssaulting();
            return;
        }

        float radius = Settings.HostilePrepareWanderRadius;
        var offset = new Vector3(
            _rng.RandfRange(-radius, radius),
            0f,
            _rng.RandfRange(-radius, radius));
        NavigateToPosition(WaveContext.RallyCenter + offset);
        _prepareNextRepickTick = tick + Settings.HostilePrepareRepickTicks;
    }

    private void EnterAssaulting()
    {
        Stop();
        _target = null;
        _lastTargetSearchTick = 0;
        SetAssaultPhase(EnemyAssaultPhase.Assaulting);
        EnsureAssaultStartNotification();
        RefreshThreatLevel();
    }

    private void NavigateToAssaultObjective()
    {
        Vector3 objective = PawnManager.Instance?.GetColonyCenter()
            ?? WaveContext?.RallyCenter
            ?? GlobalPosition;
        NavigateToPosition(objective);
    }

    private void NavigateToPosition(Vector3 targetPos)
    {
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

    private void InitializeAssaultState()
    {
        long currentTick = TimeManager.Instance?.CurrentTick ?? 0;
        if (WaveContext != null
            && WaveContext.AssaultMode == EnemyAssaultMode.DelayedAttack
            && currentTick < WaveContext.PrepareUntilTick)
        {
            SetAssaultPhase(EnemyAssaultPhase.Preparing);
            _prepareNextRepickTick = 0;
            return;
        }

        SetAssaultPhase(EnemyAssaultPhase.Assaulting);
        EnsureAssaultStartNotification();
    }

    private void SetAssaultPhase(EnemyAssaultPhase nextPhase)
    {
        if (CurrentAssaultPhase == nextPhase)
            return;

        CurrentAssaultPhase = nextPhase;
        if (nextPhase != EnemyAssaultPhase.Preparing)
            _prepareNextRepickTick = 0;
    }

    private void EnsureAssaultStartNotification()
    {
        if (WaveContext == null || WaveContext.AssaultNotificationSent)
            return;

        WaveContext.AssaultNotificationSent = true;
        string desc = WaveContext.AssaultMode == EnemyAssaultMode.DelayedAttack
            ? "\u654c\u5bf9\u5355\u4f4d\u7ed3\u675f\u96c6\u7ed3\uff0c\u5f00\u59cb\u5411\u6b96\u6c11\u5730\u53d1\u8d77\u8fdb\u653b\u3002"
            : "\u654c\u5bf9\u5355\u4f4d\u5df2\u7ecf\u5f00\u59cb\u5411\u6b96\u6c11\u5730\u63a8\u8fdb\u3002";
        EventBus.FireIncidentTriggered("_assault_start", "\u654c\u4eba\u5f00\u59cb\u8fdb\u653b\uff01", desc);
    }

    private void RefreshThreatLevel()
    {
        EndfieldZero.Storyteller.Storyteller.Instance?.RefreshThreatLevel();
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

    private void StartAttackAnimation(Vector3 facing)
    {
        facing.Y = 0f;
        if (facing.LengthSquared() > 0.0001f)
            _attackFacing = facing.Normalized();

        _animController.Update(_attackFacing, PawnAnimController.PawnAnimState.Attacking);
        _attackAnimTimeRemaining = GetCurrentAnimationLengthOrDefault();
    }

    private float GetCurrentAnimationLengthOrDefault()
    {
        if (_animPlayer == null)
            return DefaultAttackAnimDurationSeconds;

        string currentAnimation = _animPlayer.CurrentAnimation;
        if (!string.IsNullOrEmpty(currentAnimation) && _animPlayer.HasAnimation(currentAnimation))
            return (float)_animPlayer.GetAnimation(currentAnimation).Length;

        return DefaultAttackAnimDurationSeconds;
    }

    public void Die(string cause)
    {
        if (!IsAlive) return;
        IsAlive = false;
        Stop();
        GD.Print($"[EnemyPawn] {Data.PawnName} died: {cause}");
        EventBus.FirePawnDied(Data.Id);
        RefreshThreatLevel();

        var timer = GetTree().CreateTimer(0.5);
        timer.Timeout += QueueFree;
    }

    public Vector3 GetCombatAimPoint()
    {
        return GlobalPosition + new Vector3(0f, 0.85f, 0f);
    }

    // --- Visuals ---

    private void CreateHostileIndicator()
    {
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
        _hostileIndicator.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(_hostileIndicator);
    }

    private void UpdateSpritePresentation()
    {
        if (_sprite == null) return;

        bool angled3D = GameCamera.Instance?.IsAngledView == true;
        _sprite.NoDepthTest = angled3D;
        _sprite.RenderPriority = angled3D ? 10 : 0;
        UpdateSpriteAnchor();
    }

    private void UpdateNameLabel()
    {
        PawnNameLabel3D.Update(
            _nameLabel,
            _nameShadowLabel,
            GetViewport()?.GetCamera3D(),
            GlobalPosition,
            Data?.PawnName,
            IsAlive);
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
