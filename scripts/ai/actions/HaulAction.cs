using System.Linq;
using EndfieldZero.Core;
using EndfieldZero.Items;
using EndfieldZero.Jobs;
using EndfieldZero.Pathfinding;
using EndfieldZero.Pawn;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// HaulAction — picks up a ground item and carries it to a stockpile zone.
///
/// Workflow:
///   1. Find a Haul job with a valid item
///   2. Walk to the item
///   3. Pick up (set item state to BeingCarried)
///   4. Walk to destination stockpile cell
///   5. Drop (set item state to InStockpile, move to dest)
///
/// This is a separate action from DoJobAction because hauling has
/// a two-phase movement (pickup → delivery) unlike single-target jobs.
/// </summary>
public class HaulAction : AIAction
{
    public override string Name => _phase == HaulPhase.Delivering
        ? $"搬运:{_carriedItem?.Def.DisplayName ?? "?"}"
        : "准备搬运";

    private Job _haulJob;
    private ItemStack _carriedItem;
    private bool _isComplete;
    private Pawn.Pawn _cachedPawn;

    private enum HaulPhase { GoToItem, PickingUp, GoToDest, Delivering, Done }
    private HaulPhase _phase = HaulPhase.GoToItem;

    private static float ReachDist => 1.5f * Settings.BlockPixelSize;

    public override float[] GetQueryVector(AIContext context)
    {
        // Lower priority than regular jobs, but still useful
        float haulWeight = 0f;
        if (ItemManager.Instance != null)
        {
            var groundItems = ItemManager.Instance.GetGroundItems();
            foreach (var _ in groundItems) { haulWeight = 1f; break; }
        }

        return new float[]
        {
            -context.HungerUrgency * 0.4f,
            -context.RestUrgency * 0.3f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            haulWeight * 1.2f,    // Lower than DoJobAction's 2.0
            0.2f,
            0.1f,
        };
    }

    public override bool CanExecute(AIContext context)
    {
        if (context.Pawn.Needs.Hunger < 15f || context.Pawn.Needs.Rest < 10f)
            return false;

        // Check for available haul jobs
        if (JobSystem.Instance == null) return false;
        return JobSystem.Instance.AllJobs.Any(j =>
            j.JobType == "Haul" && j.IsAvailable);
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isComplete = false;
        _phase = HaulPhase.GoToItem;
        _cachedPawn = context.Pawn;

        // Claim a haul job
        _haulJob = JobSystem.Instance?.AllJobs
            .Where(j => j.JobType == "Haul" && j.IsAvailable)
            .OrderBy(j => context.Pawn.GlobalPosition.DistanceTo(j.TargetWorldPos))
            .FirstOrDefault();

        if (_haulJob == null)
        {
            _isComplete = true;
            return;
        }

        _haulJob.Reserve(context.Pawn.GetInstanceId().GetHashCode());
        _haulJob.Start();

        // Get the item
        _carriedItem = ItemManager.Instance?.GetItem(_haulJob.HaulItemId);
        if (_carriedItem == null)
        {
            _haulJob.Fail();
            _isComplete = true;
            return;
        }

        // Navigate to item
        NavigateTo(context.Pawn, _carriedItem.GlobalPosition);
    }

    public override void Execute(AIContext context)
    {
        if (_isComplete) return;

        var pawn = context.Pawn;

        switch (_phase)
        {
            case HaulPhase.GoToItem:
                if (_carriedItem == null || !IsInstanceValid(_carriedItem))
                {
                    _haulJob?.Fail();
                    _isComplete = true;
                    return;
                }

                if (pawn.GlobalPosition.DistanceTo(_carriedItem.GlobalPosition) < ReachDist)
                {
                    // Pick up
                    _carriedItem.State = ItemState.BeingCarried;
                    _phase = HaulPhase.GoToDest;

                    // Navigate to destination
                    float px = Settings.BlockPixelSize;
                    var dest = new Vector3(
                        (_haulJob.HaulDestCoord.X + 0.5f) * px,
                        0f,
                        (_haulJob.HaulDestCoord.Y + 0.5f) * px);
                    NavigateTo(pawn, dest);
                }
                break;

            case HaulPhase.GoToDest:
            {
                float px = Settings.BlockPixelSize;
                var dest = new Vector3(
                    (_haulJob.HaulDestCoord.X + 0.5f) * px,
                    0f,
                    (_haulJob.HaulDestCoord.Y + 0.5f) * px);

                if (pawn.GlobalPosition.DistanceTo(dest) < ReachDist)
                {
                    // Drop at destination
                    if (_carriedItem != null && IsInstanceValid(_carriedItem))
                    {
                        _carriedItem.MoveTo(_haulJob.HaulDestCoord);
                        _carriedItem.State = ItemState.InStockpile;
                    }

                    _haulJob.Complete();
                    _phase = HaulPhase.Done;
                    _isComplete = true;

                    // Mood boost
                    pawn.Mood.AddThought("finished_haul", "搬运完成", 3f,
                        TimeManager.TicksPerHour);
                }
                break;
            }
        }
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    public override void OnStop()
    {
        if (_haulJob != null && _haulJob.Status != JobStatus.Completed)
        {
            if (_carriedItem != null && IsInstanceValid(_carriedItem) &&
                _carriedItem.State == ItemState.BeingCarried)
            {
                _carriedItem.State = ItemState.OnGround;
            }

            _haulJob.Release();
        }

        _haulJob = null;
        _carriedItem = null;
        _cachedPawn = null;
        base.OnStop();
    }

    public override bool ShouldInterrupt(AIContext context)
    {
        // Don't interrupt while carrying an item (finish the delivery)
        if (_phase == HaulPhase.GoToDest)
        {
            if (context.Pawn.Needs.Hunger < 10f || context.Pawn.Needs.Rest < 5f)
                return true;
            return false;
        }
        return true;
    }

    private static bool IsInstanceValid(GodotObject obj)
    {
        return GodotObject.IsInstanceValid(obj);
    }

    private void NavigateTo(Pawn.Pawn pawn, Vector3 target)
    {
        if (PathfindingService.Instance != null)
        {
            var startBlock = PathfindingService.WorldToBlock(pawn.GlobalPosition);
            var endBlock = PathfindingService.WorldToBlock(target);
            var path = PathfindingService.Instance.FindPath(startBlock, endBlock);
            var worldPath = PathfindingService.PathToWorld(path);
            if (worldPath != null && worldPath.Count > 0)
            {
                pawn.PlayerFollowPath(worldPath);
                return;
            }
        }
        pawn.PlayerMoveTo(target);
    }
}
