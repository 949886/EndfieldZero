using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.Pathfinding;
using EndfieldZero.Pawn;
using EndfieldZero.Research;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// Claims and executes a job from the JobSystem.
/// Walks to the job target, then performs work until completion.
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
            -context.HungerUrgency * 0.3f,
            -context.RestUrgency * 0.2f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            jobWeight * 2.0f,
            0.3f,
            0.2f,
        };
    }

    public override bool CanExecute(AIContext context)
    {
        if (context.Pawn.Needs.Hunger < 15f || context.Pawn.Needs.Rest < 10f)
            return false;
        return JobSystem.Instance?.HasAvailableNonHaulJobs(context.Pawn) ?? false;
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isAtTarget = false;
        _isComplete = false;

        _claimedJob = JobSystem.Instance?.FindBestNonHaulJob(context.Pawn);
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
        if (_isComplete || _claimedJob == null)
            return;

        if (!_isAtTarget)
        {
            float dist = context.Pawn.GlobalPosition.DistanceTo(_claimedJob.TargetWorldPos);
            if (dist < WorkReachDist)
            {
                _isAtTarget = true;
                _claimedJob.Start();
                context.Pawn.Stop();
                context.Pawn.SetWorkTarget(_claimedJob.TargetWorldPos, ResolveWorkAnimation(_claimedJob.JobType));
            }
            else if (!context.Pawn.IsMoving)
            {
                GD.Print($"[AI] {context.Pawn.Data.PawnName} path to job blocked. Cancelling.");
                _claimedJob.Fail();
                _isComplete = true;
                return;
            }

            return;
        }

        context.Pawn.SetWorkTarget(_claimedJob.TargetWorldPos, ResolveWorkAnimation(_claimedJob.JobType));
        _claimedJob.TicksWorked += CalculateWorkContribution(context);

        if (_claimedJob.TicksRemaining <= 0f)
            CompleteJob(context);
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    public override bool ShouldInterrupt(AIContext context)
    {
        if (_claimedJob == null)
            return true;

        if (context.Pawn.Health != null && context.Pawn.Health.HpPercent < 0.3f)
            return true;

        if (context.NearbyEnemyCount > 0)
            return true;

        if (_claimedJob.Status == JobStatus.Reserved || _claimedJob.Status == JobStatus.InProgress)
            return context.Pawn.Needs.Hunger < 10f || context.Pawn.Needs.Rest < 5f;

        return true;
    }

    public override void OnStop()
    {
        if (_claimedJob != null && _claimedJob.Status != JobStatus.Completed)
        {
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

    private float CalculateWorkContribution(AIContext context)
    {
        float workContribution = 1f;

        if (!string.IsNullOrEmpty(_claimedJob.RequiredSkill))
        {
            float skillLevel = context.Pawn.Data.GetStat(_claimedJob.RequiredSkill);

            if (skillLevel >= 10f && context.CurrentTick % 3 == 0)
                workContribution += 1f;

            if (skillLevel >= 15f && context.CurrentTick % 2 == 0)
                workContribution += 1f;

            context.Pawn.Data.AddExperience(_claimedJob.RequiredSkill, _claimedJob.XpPerTick);
        }

        float moodMod = context.Pawn.Mood.GetWorkSpeedModifier();
        if (moodMod > 1f && context.CurrentTick % 2 == 0)
            workContribution += moodMod - 1f;

        if (_claimedJob.JobType == "Construct")
            workContribution *= TechnologyManager.Instance?.ConstructionSpeedMultiplier ?? 1f;

        return workContribution;
    }

    private void CompleteJob(AIContext context)
    {
        _claimedJob.Complete();
        context.Pawn.ClearWorkTarget();

        GD.Print($"[AI] {context.Pawn.Data.PawnName} completed: {_claimedJob.DisplayName}");

        ApplyJobResult(context);
        EventBus.FireJobCompleted(_claimedJob.Id);

        string label = _claimedJob.JobType switch
        {
            "Mine" => "采掘工作完成",
            "Construct" => "建造任务完成",
            "Grow" => "播种工作完成",
            "Harvest" => "收获工作完成",
            "Research" => "研究推进顺利",
            _ => "工作完成",
        };
        context.Pawn.Mood.AddThought("finished_work", label, 5f, TimeManager.TicksPerHour * 2);

        if (_claimedJob.JobType != "Research")
            UI.ToolModeManager.Instance?.OnJobCompleted(_claimedJob.TargetBlockCoord);

        _claimedJob = null;
        _isComplete = true;
    }

    private void ApplyJobResult(AIContext context)
    {
        switch (_claimedJob.JobType)
        {
            case "Mine":
            {
                var coord = _claimedJob.TargetBlockCoord;

                if (WorldManager.Instance != null)
                {
                    var block = WorldManager.Instance.GetBlock(coord.X, coord.Y);
                    var dropId = Items.ItemRegistry.BlockDropItemId(block.TypeId);
                    if (dropId != null && Items.ItemManager.Instance != null)
                    {
                        int dropCount = block.TypeId switch
                        {
                            World.BlockRegistry.TreeId or
                            World.BlockRegistry.ConiferTreeId or
                            World.BlockRegistry.BirchTreeId or
                            World.BlockRegistry.JungleTreeId or
                            World.BlockRegistry.AcaciaTreeId => 3,
                            World.BlockRegistry.OreDiamondId => 1,
                            _ => 2,
                        };
                        Items.ItemManager.Instance.SpawnItem(coord, dropId, dropCount);
                    }

                    WorldManager.Instance.SetBlock(coord.X, coord.Y, Block.Air);
                }
                break;
            }

            case "Construct":
                if (_claimedJob.BlueprintId >= 0)
                {
                    Building.BlueprintSystem.Instance?.CompleteBlueprint(_claimedJob.BlueprintId);
                }
                else if (WorldManager.Instance != null)
                {
                    var coord = _claimedJob.TargetBlockCoord;
                    WorldManager.Instance.SetBlock(coord.X, coord.Y,
                        new Block(World.BlockRegistry.StoneWallId));
                }
                break;

            case "Grow":
                if (Farming.CropManager.Instance != null)
                {
                    var coord = _claimedJob.TargetBlockCoord;
                    var cropDef = Farming.CropRegistry.Instance.GetRandom();
                    if (cropDef != null)
                        Farming.CropManager.Instance.PlantCrop(cropDef, coord);
                }
                break;

            case "Harvest":
            {
                var coord = _claimedJob.TargetBlockCoord;

                if (Farming.CropManager.Instance != null && Items.ItemManager.Instance != null)
                {
                    var crop = Farming.CropManager.Instance.GetCropAt(coord);
                    if (crop != null)
                    {
                        var itemId = Items.ItemRegistry.CropDropItemId(crop.Def.Id);
                        if (itemId != null)
                            Items.ItemManager.Instance.SpawnItem(coord, itemId, crop.Def.HarvestYield);
                    }
                    Farming.CropManager.Instance.RemoveCrop(coord);
                }
                break;
            }

            case "Research":
                TechnologyManager.Instance?.OnResearchJobCompleted(
                    _claimedJob.Id,
                    _claimedJob.ResearchTechnologyId,
                    _claimedJob.TicksWorked);
                break;
        }
    }

    private static PawnVisualAction ResolveWorkAnimation(string jobType)
    {
        return jobType switch
        {
            "Grow" => PawnVisualAction.Watering,
            _ => PawnVisualAction.Dig,
        };
    }
}
