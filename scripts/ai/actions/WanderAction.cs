using EndfieldZero.Core;
using EndfieldZero.Pathfinding;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// Wander action — the pawn moves to a random nearby position when idle.
/// Low priority; only selected when there's nothing else to do.
///
/// Q vector: high on Idleness, moderate on Joy (wandering is slightly joyful),
/// low on everything else.
/// </summary>
public class WanderAction : AIAction
{
    public override string Name => "Wander";

    private Vector3 _wanderTarget;
    private bool _reachedTarget;
    private long _waitUntilTick;
    private bool _isWaiting;

    public override float[] GetQueryVector(AIContext context)
    {
        return new float[]
        {
            0.0f,   // Hunger — doesn't care
            0.0f,   // Rest
            0.15f,  // Joy — slight relevance
            0.0f,   // Comfort
            0.05f,  // Beauty — slight
            0.1f,   // Social
            0.0f,   // JobAvailable — doesn't care about jobs
            0.1f,   // Safety
            0.8f,   // Idleness — very high: only wander when idle
        };
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _reachedTarget = false;
        _isWaiting = false;
        PickNewTarget(context);
    }

    public override void Execute(AIContext context)
    {
        if (_isWaiting)
        {
            // Wait at the destination for a bit before completing
            if (context.CurrentTick >= _waitUntilTick)
            {
                _reachedTarget = true;
            }
            return;
        }

        // Check if we've arrived
        if (!Owner.IsMoving)
        {
            // Arrived — wait 2-5 seconds
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _waitUntilTick = context.CurrentTick + rng.RandiRange(120, 300);
            _isWaiting = true;

            // Small joy boost from wandering
            Owner.Needs.Joy = Mathf.Min(100f, Owner.Needs.Joy + 0.5f);
        }
    }

    public override bool IsComplete(AIContext context)
    {
        return _reachedTarget;
    }

    private void PickNewTarget(AIContext context)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        float radius = Settings.PawnWanderRadius;
        float dx = rng.RandfRange(-radius, radius);
        float dz = rng.RandfRange(-radius, radius);

        _wanderTarget = Owner.GlobalPosition + new Vector3(dx, 0, dz);

        // Try A* pathfinding
        if (PathfindingService.Instance != null)
        {
            var startBlock = PathfindingService.WorldToBlock(Owner.GlobalPosition);
            var goalBlock = PathfindingService.WorldToBlock(_wanderTarget);
            var path = PathfindingService.Instance.FindPath(startBlock, goalBlock);
            var worldPath = PathfindingService.PathToWorld(path);

            if (worldPath != null && worldPath.Count > 1)
            {
                Owner.FollowPath(worldPath);
                return;
            }
        }

        // Fallback: direct movement
        Owner.MoveTo(_wanderTarget);
    }
}
