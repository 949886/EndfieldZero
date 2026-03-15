using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.Pathfinding;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// DoJobAction — claims and executes a job from the JobSystem.
/// Walks to the job target, then performs work ticks until completion.
///
/// Q vector: very sensitive to JobAvailability dimension.
/// Also considers skill match for the best available job.
/// </summary>
public class DoJobAction : AIAction
{
    public override string Name => "DoJob";

    private Job _claimedJob;
    private bool _isAtTarget;
    private bool _isComplete;

    private const float WorkReachDist = 64f;  // How close the pawn needs to be to work

    public override float[] GetQueryVector(AIContext context)
    {
        // Only scores high when jobs are available
        // Moderate need sensitivity means pawns won't work if starving
        float jobWeight = context.JobAvailability;

        return new float[]
        {
            -context.HungerUrgency * 0.3f,   // Negative: don't work if starving
            -context.RestUrgency * 0.2f,      // Negative: don't work if exhausted
            0.0f,                              // Joy
            0.0f,                              // Comfort
            0.0f,                              // Beauty
            0.0f,                              // Social
            jobWeight * 2.0f,                  // Jobs — very high when available
            0.3f,                              // Safety — prefer safe areas
            0.2f,                              // Idleness
        };
    }

    public override bool CanExecute(AIContext context)
    {
        // Can't work if critical needs
        if (context.Pawn.Needs.Hunger < 15f || context.Pawn.Needs.Rest < 10f)
            return false;

        return context.JobAvailability > 0f;
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isAtTarget = false;
        _isComplete = false;

        // Claim the best job
        _claimedJob = JobSystem.Instance?.FindBestJob(context.Pawn);
        if (_claimedJob == null)
        {
            _isComplete = true;
            return;
        }

        _claimedJob.Reserve(context.Pawn.Data.Id);

        GD.Print($"[AI] {context.Pawn.Data.PawnName} claimed job: {_claimedJob.DisplayName} at {_claimedJob.TargetBlockCoord}");

        // Navigate to target
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
            }
            return;
        }

        // Do work
        _claimedJob.TicksWorked++;

        // Grant XP
        if (!string.IsNullOrEmpty(_claimedJob.RequiredSkill))
        {
            float skillLevel = context.Pawn.Data.GetStat(_claimedJob.RequiredSkill);
            float speedMod = 1f + skillLevel * 0.05f;  // Higher skill = slightly faster

            // Extra tick for high skill (simulates faster work)
            if (skillLevel >= 10f && context.CurrentTick % 3 == 0)
                _claimedJob.TicksWorked++;

            context.Pawn.Data.AddExperience(_claimedJob.RequiredSkill, _claimedJob.XpPerTick);
        }

        // Mood modifier from work speed
        float moodMod = context.Pawn.Mood.GetWorkSpeedModifier();
        if (moodMod > 1f && context.CurrentTick % 2 == 0)
            _claimedJob.TicksWorked++;  // Inspired pawns work faster

        // Check completion
        if (_claimedJob.TicksRemaining <= 0)
        {
            CompleteJob(context);
        }
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    public override void OnStop()
    {
        // Cancel claimed job if we're interrupted
        if (_claimedJob != null && _claimedJob.Status != JobStatus.Completed)
        {
            _claimedJob.Cancel();
            _claimedJob = null;
        }
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

        // Fallback: direct move
        context.Pawn.MoveTo(_claimedJob.TargetWorldPos);
    }

    private void CompleteJob(AIContext context)
    {
        _claimedJob.Complete();

        GD.Print($"[AI] {context.Pawn.Data.PawnName} completed: {_claimedJob.DisplayName}");

        // Apply job results
        ApplyJobResult(context);

        EventBus.FireJobCompleted(_claimedJob.Id);

        // Positive mood from finishing work
        context.Pawn.Mood.AddThought("finished_work", "完成了工作", 5f, Core.TimeManager.TicksPerHour * 2);

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
                // Future: place a constructed block/building
                break;

            case "Haul":
                // Future: move item from A to B
                break;
        }
    }
}
