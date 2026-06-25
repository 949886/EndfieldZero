using EndfieldZero.Combat;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Pawn;

public sealed class MiyuCombatController : CharacterCombatController
{
    private enum MiyuState
    {
        Idle,
        Move,
        MoveStop,
        Work,
        AttackStart,
        AttackLoop,
        AttackDelay,
        AttackEnd,
        Death,
    }

    private MiyuState _state = MiyuState.Idle;
    private string _currentAnimation = string.Empty;
    private double _stateElapsed;
    private Vector3 _lastFacing = Vector3.Forward;
    private EnemyPawn _attackTarget;
    private WeaponDef _attackWeapon;
    private bool _attackFired;
    private bool _useKneelSet;

    public override void Tick(double delta, Vector3 desiredDirection, PawnVisualAction desiredAction)
    {
        UpdateFacing(desiredDirection);

        if (_state == MiyuState.Death)
            return;

        if (IsBusy)
        {
            AdvanceBusyState(delta);
            return;
        }

        _stateElapsed += delta;
        ApplyLocomotion(desiredAction);
    }

    public override bool TryStartAttack(EnemyPawn target, WeaponDef weapon)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || target.Health?.IsDead == true)
            return false;

        if (AnimationPlayer == null || Definition == null || _state == MiyuState.Death || IsBusy)
            return false;

        _attackTarget = target;
        _attackWeapon = weapon;
        _attackFired = false;
        _useKneelSet = ShouldUseKneelAttack(target, weapon);
        TransitionTo(MiyuState.AttackStart, GetAttackStartClip());
        IsBusy = true;
        return true;
    }

    public override void CancelAttack()
    {
        if (_state == MiyuState.Death)
            return;

        _attackTarget = null;
        _attackWeapon = null;
        _attackFired = false;
        _useKneelSet = false;

        if (IsBusy)
        {
            IsBusy = false;
            TransitionTo(MiyuState.Idle, Definition?.IdleAnimation);
        }
    }

    public override void OnDeath()
    {
        _attackTarget = null;
        _attackWeapon = null;
        _attackFired = false;
        _useKneelSet = false;
        IsBusy = false;
        TransitionTo(MiyuState.Death, Definition?.DeathAnimation);
    }

    private void ApplyLocomotion(PawnVisualAction desiredAction)
    {
        switch (desiredAction)
        {
            case PawnVisualAction.Move:
                TransitionTo(MiyuState.Move, Definition?.MoveAnimation);
                break;
            case PawnVisualAction.Dig:
                TransitionTo(MiyuState.Work, Definition?.WorkAnimation);
                break;
            case PawnVisualAction.Watering:
                TransitionTo(MiyuState.Work, string.IsNullOrWhiteSpace(Definition?.WateringAnimation)
                    ? Definition?.WorkAnimation
                    : Definition.WateringAnimation);
                break;
            default:
                if (_state == MiyuState.Move && HasAnimation(Definition?.MoveStopAnimation))
                    TransitionTo(MiyuState.MoveStop, Definition.MoveStopAnimation);
                else if (_state == MiyuState.MoveStop && _stateElapsed < GetCurrentAnimationLength())
                    return;
                else
                    TransitionTo(MiyuState.Idle, Definition?.IdleAnimation);
                break;
        }
    }

    private void AdvanceBusyState(double delta)
    {
        _stateElapsed += delta;

        switch (_state)
        {
            case MiyuState.AttackStart:
                if (_stateElapsed >= GetCurrentAnimationLength())
                    TransitionTo(MiyuState.AttackLoop, GetAttackLoopClip());
                break;

            case MiyuState.AttackLoop:
                if (!_attackFired && _stateElapsed >= GetAttackFireTime())
                {
                    FireAttackDamage();
                    _attackFired = true;
                }

                if (_stateElapsed >= GetCurrentAnimationLength())
                    TransitionTo(MiyuState.AttackDelay, GetAttackDelayClip());
                break;

            case MiyuState.AttackDelay:
                if (_stateElapsed >= GetCurrentAnimationLength())
                    TransitionTo(MiyuState.AttackEnd, GetAttackEndClip());
                break;

            case MiyuState.AttackEnd:
                if (_stateElapsed >= GetCurrentAnimationLength())
                {
                    IsBusy = false;
                    _attackTarget = null;
                    _attackWeapon = null;
                    _attackFired = false;
                    _useKneelSet = false;
                    TransitionTo(MiyuState.Idle, Definition?.IdleAnimation);
                }
                break;
        }
    }

    private void FireAttackDamage()
    {
        if (_attackTarget == null || !GodotObject.IsInstanceValid(_attackTarget) || _attackTarget.Health?.IsDead == true)
            return;

        if (_attackWeapon?.IsRanged == true)
        {
            PreparedRangedShot shot = DamageSystem.PrepareRangedAttack(Pawn, _attackTarget);
            if (shot != null)
                Pawn.FireRangedShot(shot);
            return;
        }

        DamageSystem.Attack(Pawn, _attackTarget);
    }

    private void UpdateFacing(Vector3 desiredDirection)
    {
        Vector3 facing = desiredDirection;

        if (IsBusy && _attackTarget != null && GodotObject.IsInstanceValid(_attackTarget))
            facing = _attackTarget.GlobalPosition - Pawn.GlobalPosition;

        facing.Y = 0f;
        if (facing.LengthSquared() > 0.0001f)
            _lastFacing = facing.Normalized();

        if (VisualRoot == null || _lastFacing.LengthSquared() <= 0.0001f)
            return;

        float yaw = Mathf.Atan2(_lastFacing.X, _lastFacing.Z) + Mathf.DegToRad(Pawn.ModelYawOffsetDegrees);
        Vector3 rotation = VisualRoot.Rotation;
        rotation.Y = yaw;
        VisualRoot.Rotation = rotation;
    }

    private bool ShouldUseKneelAttack(EnemyPawn target, WeaponDef weapon)
    {
        if (Definition == null || weapon == null || !weapon.IsRanged || !Definition.UseKneelAttack)
            return false;

        float distBlocks = Pawn.GlobalPosition.DistanceTo(target.GlobalPosition) / Settings.BlockPixelSize;
        return distBlocks >= Definition.KneelMinDistanceBlocks
            && HasAnimation(Definition.KneelAttackStartAnimation)
            && HasAnimation(Definition.KneelAttackLoopAnimation)
            && HasAnimation(Definition.KneelAttackDelayAnimation)
            && HasAnimation(Definition.KneelAttackEndAnimation);
    }

    private string GetAttackStartClip() => _useKneelSet ? Definition?.KneelAttackStartAnimation : Definition?.AttackStartAnimation;

    private string GetAttackLoopClip() => _useKneelSet ? Definition?.KneelAttackLoopAnimation : Definition?.AttackLoopAnimation;

    private string GetAttackDelayClip() => _useKneelSet ? Definition?.KneelAttackDelayAnimation : Definition?.AttackDelayAnimation;

    private string GetAttackEndClip() => _useKneelSet ? Definition?.KneelAttackEndAnimation : Definition?.AttackEndAnimation;

    private double GetAttackFireTime() => _useKneelSet ? Definition?.KneelAttackFireTimeSeconds ?? 0.15f : Definition?.AttackFireTimeSeconds ?? 0.15f;

    private void TransitionTo(MiyuState nextState, string animationName)
    {
        _state = nextState;
        _stateElapsed = 0d;

        if (string.IsNullOrWhiteSpace(animationName) || !HasAnimation(animationName))
            return;

        if (_currentAnimation == animationName && AnimationPlayer.IsPlaying())
            return;

        _currentAnimation = animationName;
        AnimationPlayer.Play(animationName);
    }

    private bool HasAnimation(string animationName)
    {
        return AnimationPlayer != null
            && !string.IsNullOrWhiteSpace(animationName)
            && AnimationPlayer.HasAnimation(animationName);
    }

    private double GetCurrentAnimationLength()
    {
        if (!HasAnimation(_currentAnimation))
            return 0d;

        return AnimationPlayer.GetAnimation(_currentAnimation)?.Length ?? 0d;
    }
}
