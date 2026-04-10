using System.Linq;
using EndfieldZero.Combat;
using EndfieldZero.Core;
using EndfieldZero.Managers;
using EndfieldZero.Pathfinding;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// CombatAction — engages in combat with a hostile target.
///
/// Colonists: auto-triggered when Safety &lt; 1 or IsDrafted.
/// Hostiles:  always active when enemies in range.
///
/// Melee: move into range → attack → cooldown → repeat
/// Ranged: keep distance → shoot → cooldown → repeat
/// </summary>
public class CombatAction : AIAction
{
    public override string Name => _target != null
        ? $"战斗:{_target.Data.PawnName}"
        : "战斗";

    private Pawn.Pawn _target;
    private int _attackCooldown;
    private bool _isComplete;

    public override float[] GetQueryVector(AIContext context)
    {
        float threat = 1f - context.Safety;
        float draftBoost = context.Pawn.IsDrafted ? 2f : 0f;

        return new float[]
        {
            -context.HungerUrgency * 0.1f,
            -context.RestUrgency * 0.1f,
            0f, 0f, 0f, 0f,
            0f,                            // not a job
            threat * 3f + draftBoost,      // very high when enemies near or drafted
            0f,
        };
    }

    public override bool CanExecute(AIContext context)
    {
        // Drafted pawns always can fight
        if (context.Pawn.IsDrafted) return true;

        // Auto-fight when enemies nearby
        if (context.NearbyEnemyCount > 0) return true;

        // Hostile pawns always fight
        if (context.Pawn.Data.IsHostile) return true;

        return false;
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isComplete = false;
        _attackCooldown = 0;
        _target = FindTarget(context.Pawn);

        if (_target == null)
        {
            _isComplete = true;
            return;
        }

        NavigateToTarget(context.Pawn);
    }

    public override void Execute(AIContext context)
    {
        if (_isComplete) return;
        var pawn = context.Pawn;

        // Validate target
        if (_target == null || !GodotObject.IsInstanceValid(_target) ||
            !_target.IsAlive || (_target.Health?.IsDead ?? true))
        {
            _target = FindTarget(pawn);
            if (_target == null)
            {
                _isComplete = true;
                return;
            }
            NavigateToTarget(pawn);
        }

        // Attack cooldown
        if (_attackCooldown > 0)
        {
            _attackCooldown--;
            return;
        }

        // Check range
        if (DamageSystem.IsInRange(pawn, _target))
        {
            // In range — attack
            pawn.Stop();
            pawn.SetWorkTarget(_target.GlobalPosition);
            DamageSystem.Attack(pawn, _target);

            var weapon = DamageSystem.GetWeapon(pawn);
            _attackCooldown = weapon.CooldownTicks;

            // If target died, find new target
            if (_target.Health.IsDead)
            {
                pawn.ClearWorkTarget();
                _target = FindTarget(pawn);
                if (_target == null)
                {
                    _isComplete = true;
                    return;
                }
                NavigateToTarget(pawn);
            }
        }
        else
        {
            // Out of range — move closer
            pawn.ClearWorkTarget();
            NavigateToTarget(pawn);
        }
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    public override void OnStop()
    {
        Owner?.ClearWorkTarget();
        _target = null;
        base.OnStop();
    }

    public override bool ShouldInterrupt(AIContext context)
    {
        // Don't interrupt combat unless we're fleeing
        if (context.Pawn.Health != null && context.Pawn.Health.HpPercent < 0.3f)
            return true; // Let FleeAction take over

        // Drafted pawns never auto-interrupt combat
        if (context.Pawn.IsDrafted) return false;

        return false;
    }

    private Pawn.Pawn FindTarget(Pawn.Pawn self)
    {
        // Player-issued attack target takes priority
        if (self.AttackTargetPawn != null && GodotObject.IsInstanceValid(self.AttackTargetPawn)
            && self.AttackTargetPawn.IsAlive)
        {
            return self.AttackTargetPawn;
        }

        string myFaction = self.Data.Faction;
        float detectionRange = 20f * Settings.BlockPixelSize;

        Pawn.Pawn best = null;
        float bestDist = float.MaxValue;

        if (PawnManager.Instance == null) return null;

        foreach (var other in PawnManager.Instance.GetAllPawns())
        {
            if (other == self || !other.IsAlive) continue;
            if (other.Health?.IsDead ?? true) continue;

            // Faction check: attack enemies
            bool isEnemy = false;
            if (myFaction == "Colony" || myFaction == "Neutral")
                isEnemy = other.Data.IsHostile;
            else if (myFaction == "Hostile" || myFaction == "Animal")
                isEnemy = other.Data.Faction == "Colony";

            if (!isEnemy) continue;

            float dist = self.GlobalPosition.DistanceTo(other.GlobalPosition);
            if (dist < detectionRange && dist < bestDist)
            {
                bestDist = dist;
                best = other;
            }
        }

        return best;
    }

    private void NavigateToTarget(Pawn.Pawn pawn)
    {
        if (_target == null) return;

        var weapon = DamageSystem.GetWeapon(pawn);
        Vector3 targetPos = _target.GlobalPosition;

        // Ranged: stay at ~80% max range
        if (weapon.IsRanged)
        {
            float desiredDist = weapon.Range * Settings.BlockPixelSize * 0.6f;
            Vector3 dir = (pawn.GlobalPosition - targetPos).Normalized();
            targetPos = _target.GlobalPosition + dir * desiredDist;
        }

        if (PathfindingService.Instance != null)
        {
            var start = PathfindingService.WorldToBlock(pawn.GlobalPosition);
            var end = PathfindingService.WorldToBlock(targetPos);
            var path = PathfindingService.Instance.FindPath(start, end);
            var worldPath = PathfindingService.PathToWorld(path);
            if (worldPath != null && worldPath.Count > 0)
            {
                pawn.FollowPath(worldPath);
                return;
            }
        }
        pawn.MoveTo(targetPos);
    }
}
