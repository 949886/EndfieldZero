using EndfieldZero.Core;
using EndfieldZero.Managers;
using EndfieldZero.Pathfinding;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// FleeAction — flee from combat when HP is critical.
///
/// For colonists: flee to safe zone when HP &lt; 30%
/// For hostiles:  flee off map edge when HP &lt; 30% → despawn
/// </summary>
public class FleeAction : AIAction
{
    public override string Name => "逃跑";

    private bool _isComplete;
    private Vector3 _fleeTarget;

    public override float[] GetQueryVector(AIContext context)
    {
        float hpPct = context.Pawn.Health?.HpPercent ?? 1f;
        float desperation = hpPct < 0.3f ? 5f : 0f;

        return new float[]
        {
            0f, 0f, 0f, 0f, 0f, 0f,
            0f,
            desperation,   // Safety dimension — extreme when low HP
            0f,
        };
    }

    public override bool CanExecute(AIContext context)
    {
        if (context.Pawn.Health == null) return false;

        // Flee when HP < 30% and enemies nearby
        return context.Pawn.Health.HpPercent < 0.3f && context.NearbyEnemyCount > 0;
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isComplete = false;

        var pawn = context.Pawn;
        _fleeTarget = CalculateFleeTarget(pawn);

        if (PathfindingService.Instance != null)
        {
            var start = PathfindingService.WorldToBlock(pawn.GlobalPosition);
            var end = PathfindingService.WorldToBlock(_fleeTarget);
            var path = PathfindingService.Instance.FindPath(start, end);
            var worldPath = PathfindingService.PathToWorld(path);
            if (worldPath != null && worldPath.Count > 0)
            {
                pawn.FollowPath(worldPath);
                return;
            }
        }
        pawn.MoveTo(_fleeTarget);
    }

    public override void Execute(AIContext context)
    {
        if (_isComplete) return;
        var pawn = context.Pawn;

        // Reached flee target?
        if (pawn.GlobalPosition.DistanceTo(_fleeTarget) < 2f * Settings.BlockPixelSize)
        {
            if (pawn.Data.IsHostile)
            {
                // Hostile: despawn
                GD.Print($"[Flee] {pawn.Data.PawnName} fled the map");
                pawn.Die("fled");
            }
            else
            {
                // Colonist: stop fleeing, wait for healing
                pawn.Mood.AddThought("near_death", "九死一生", -20f, TimeManager.TicksPerHour * 4);
            }
            _isComplete = true;
            return;
        }

        // If not moving, re-navigate
        if (!pawn.IsMoving)
        {
            pawn.MoveTo(_fleeTarget);
        }
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    public override void OnStop()
    {
        base.OnStop();
    }

    public override bool ShouldInterrupt(AIContext context)
    {
        // Don't interrupt fleeing unless HP recovered
        return context.Pawn.Health != null && context.Pawn.Health.HpPercent > 0.5f;
    }

    private Vector3 CalculateFleeTarget(Pawn.Pawn pawn)
    {
        // Find nearest enemy and flee in opposite direction
        Vector3 awayDir = Vector3.Forward;
        float nearestDist = float.MaxValue;

        if (PawnManager.Instance != null)
        {
            foreach (var other in PawnManager.Instance.GetAllPawns())
            {
                if (other == pawn || !other.IsAlive) continue;

                bool isEnemy;
                if (pawn.Data.IsHostile)
                    isEnemy = other.Data.Faction == "Colony";
                else
                    isEnemy = other.Data.IsHostile;

                if (!isEnemy) continue;

                float dist = pawn.GlobalPosition.DistanceTo(other.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    awayDir = (pawn.GlobalPosition - other.GlobalPosition).Normalized();
                }
            }
        }

        awayDir.Y = 0;
        if (awayDir.LengthSquared() < 0.01f)
            awayDir = Vector3.Forward;

        // Flee 30 blocks away
        return pawn.GlobalPosition + awayDir * 30f * Settings.BlockPixelSize;
    }
}
