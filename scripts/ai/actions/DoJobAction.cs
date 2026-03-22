using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.Pathfinding;
using EndfieldZero.Pawn;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// DoJobAction — claims and executes a job from the JobSystem.
/// Walks to the job target, then performs work ticks until completion.
/// Uses work animation (dig) while actively working.
///
/// Q vector: very sensitive to JobAvailability dimension.
/// </summary>
public class DoJobAction : AIAction
{
    public override string Name => _claimedJob != null ? $"Job:{_claimedJob.DisplayName}" : "DoJob";

    private Job _claimedJob;
    private bool _isAtTarget;
    private bool _isComplete;

    private static float WorkReachDist => 2f * Settings.BlockPixelSize;

    public override float[] GetQueryVector(AIContext context)
    {
        float jobWeight = context.JobAvailability;

        return new float[]
        {
            -context.HungerUrgency * 0.3f,   // Don't work if starving
            -context.RestUrgency * 0.2f,      // Don't work if exhausted
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            jobWeight * 2.0f,                  // Jobs — very high when available
            0.3f,                              // Safety
            0.2f,                              // Idleness
        };
    }

    public override bool CanExecute(AIContext context)
    {
        if (context.Pawn.Needs.Hunger < 15f || context.Pawn.Needs.Rest < 10f)
            return false;
        return context.JobAvailability > 0f;
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isAtTarget = false;
        _isComplete = false;

        _claimedJob = JobSystem.Instance?.FindBestJob(context.Pawn);
        if (_claimedJob == null)
        {
            _isComplete = true;
            return;
        }

        _claimedJob.Reserve(context.Pawn.Data.Id);
        GD.Print($"[AI] {context.Pawn.Data.PawnName} claimed job: {_claimedJob.DisplayName} at {_claimedJob.TargetBlockCoord}");
        NavigateToJob(context);
    }

    public override void Execute(AIContext context)
    {
        if (_isComplete || _claimedJob == null) return;

        // Check if we've arrived at the target
        if (!_isAtTarget)
        {
            float dist = context.Pawn.GlobalPosition.DistanceTo(_claimedJob.TargetWorldPos);
            if (dist < WorkReachDist || !context.Pawn.IsMoving)
            {
                _isAtTarget = true;
                _claimedJob.Start();
                context.Pawn.Stop();

                // Face toward job target
                context.Pawn.SetWorkTarget(_claimedJob.TargetWorldPos);
            }
            return;
        }

        // Do work — set working animation every tick
        context.Pawn.SetWorkTarget(_claimedJob.TargetWorldPos);
        _claimedJob.TicksWorked++;

        // Grant XP and speed bonuses
        if (!string.IsNullOrEmpty(_claimedJob.RequiredSkill))
        {
            float skillLevel = context.Pawn.Data.GetStat(_claimedJob.RequiredSkill);

            // High skill = faster work (extra tick every 3 ticks for 10+ skill)
            if (skillLevel >= 10f && context.CurrentTick % 3 == 0)
                _claimedJob.TicksWorked++;

            // Very high skill = even faster (extra tick every 2 ticks)
            if (skillLevel >= 15f && context.CurrentTick % 2 == 0)
                _claimedJob.TicksWorked++;

            context.Pawn.Data.AddExperience(_claimedJob.RequiredSkill, _claimedJob.XpPerTick);
        }

        // Mood-based work speed
        float moodMod = context.Pawn.Mood.GetWorkSpeedModifier();
        if (moodMod > 1f && context.CurrentTick % 2 == 0)
            _claimedJob.TicksWorked++;

        // Check completion
        if (_claimedJob.TicksRemaining <= 0)
        {
            CompleteJob(context);
        }
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    /// <summary>
    /// Prevent AI from switching away while actively working.
    /// Only allow interruption if starving/exhausted.
    /// </summary>
    public override bool ShouldInterrupt(AIContext context)
    {
        if (_isAtTarget && _claimedJob != null && _claimedJob.Status == JobStatus.InProgress)
        {
            // Only interrupt for critical needs
            return context.Pawn.Needs.Hunger < 10f || context.Pawn.Needs.Rest < 5f;
        }
        return true;
    }

    public override void OnStop()
    {
        if (_claimedJob != null && _claimedJob.Status != JobStatus.Completed)
        {
            // Release — preserves work progress so another pawn can continue
            _claimedJob.Release();
            _claimedJob = null;
        }
        Owner?.ClearWorkTarget();
        base.OnStop();
    }

    private void NavigateToJob(AIContext context)
    {
        if (PathfindingService.Instance != null)
        {
            var startBlock = PathfindingService.WorldToBlock(context.Pawn.GlobalPosition);
            var path = PathfindingService.Instance.FindPath(startBlock, _claimedJob.TargetBlockCoord);
            var worldPath = PathfindingService.PathToWorld(path);

            if (worldPath != null && worldPath.Count > 0)
            {
                context.Pawn.FollowPath(worldPath);
                return;
            }
        }
        context.Pawn.MoveTo(_claimedJob.TargetWorldPos);
    }

    private void CompleteJob(AIContext context)
    {
        _claimedJob.Complete();
        context.Pawn.ClearWorkTarget();

        GD.Print($"[AI] {context.Pawn.Data.PawnName} completed: {_claimedJob.DisplayName}");

        ApplyJobResult(context);
        EventBus.FireJobCompleted(_claimedJob.Id);

        // Positive mood
        string label = _claimedJob.JobType switch
        {
            "Mine" => "完成了挖矿",
            "Construct" => "建好了建筑",
            "Grow" => "种好了作物",
            "Harvest" => "收获了作物",
            _ => "完成了工作",
        };
        context.Pawn.Mood.AddThought("finished_work", label, 5f, TimeManager.TicksPerHour * 2);

        // Notify ToolModeManager to clear designation
        UI.ToolModeManager.Instance?.OnJobCompleted(_claimedJob.TargetBlockCoord);

        _claimedJob = null;
        _isComplete = true;
    }

    private void ApplyJobResult(AIContext context)
    {
        switch (_claimedJob.JobType)
        {
            case "Mine":
                // Remove the block from the world
                if (WorldManager.Instance != null)
                {
                    var coord = _claimedJob.TargetBlockCoord;
                    WorldManager.Instance.SetBlock(coord.X, coord.Y, Block.Air);
                }
                break;

            case "Construct":
                // Complete via BlueprintSystem (places the correct block)
                if (_claimedJob.BlueprintId >= 0)
                {
                    Building.BlueprintSystem.Instance?.CompleteBlueprint(_claimedJob.BlueprintId);
                }
                else
                {
                    // Legacy fallback: place stone wall
                    if (WorldManager.Instance != null)
                    {
                        var coord = _claimedJob.TargetBlockCoord;
                        WorldManager.Instance.SetBlock(coord.X, coord.Y,
                            new Block(World.BlockRegistry.StoneWallId));
                    }
                }
                break;

            case "Grow":
                // Plant a random crop via CropManager
                if (Farming.CropManager.Instance != null)
                {
                    var coord = _claimedJob.TargetBlockCoord;
                    var cropDef = Farming.CropRegistry.Instance.GetRandom();
                    Farming.CropManager.Instance.PlantCrop(cropDef, coord);
                }
                break;

            case "Harvest":
                // Remove the crop via CropManager
                if (Farming.CropManager.Instance != null)
                {
                    var coord = _claimedJob.TargetBlockCoord;
                    Farming.CropManager.Instance.RemoveCrop(coord);
                }
                break;

            case "Haul":
                // Future: move item
                break;
        }
    }
}

