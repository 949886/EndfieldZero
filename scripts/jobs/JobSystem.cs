using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Jobs;

/// <summary>
/// Global job queue manager. Jobs are created by game systems (mining designations,
/// construction blueprints, etc.) and claimed by pawns through their AI.
///
/// Jobs are sorted by effective priority. Pawns query for the best available job
/// they are skilled enough to take.
/// </summary>
public partial class JobSystem : Node
{
    /// <summary>All registered jobs.</summary>
    private readonly List<Job> _jobs = new();

    /// <summary>Singleton instance.</summary>
    public static JobSystem Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>Add a new job to the queue.</summary>
    public void AddJob(Job job)
    {
        _jobs.Add(job);
        EventBus.FireJobCreated(job.Id);
    }

    /// <summary>Remove a completed/failed/cancelled job.</summary>
    public void RemoveJob(int jobId)
    {
        _jobs.RemoveAll(j => j.Id == jobId);
    }

    /// <summary>Get a job by ID.</summary>
    public Job GetJob(int jobId) => _jobs.FirstOrDefault(j => j.Id == jobId);

    /// <summary>Whether there are any available jobs the pawn can do.</summary>
    public bool HasAvailableJobs(Pawn.Pawn pawn)
    {
        foreach (var job in _jobs)
        {
            if (job.IsAvailable && CanPawnDoJob(pawn, job))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Find the best available job for a pawn, considering priority and distance.
    /// Returns null if no suitable job found.
    /// </summary>
    public Job FindBestJob(Pawn.Pawn pawn)
    {
        Job best = null;
        float bestScore = float.MinValue;

        foreach (var job in _jobs)
        {
            if (!job.IsAvailable || !CanPawnDoJob(pawn, job))
                continue;

            // Score = priority - distance penalty
            float distance = pawn.GlobalPosition.DistanceTo(job.TargetWorldPos);
            float distancePenalty = distance / 1000f;  // Normalize distance
            float skillBonus = pawn.Data.GetStat(job.RequiredSkill) * 0.5f;
            float score = job.EffectivePriority + skillBonus - distancePenalty;

            if (score > bestScore)
            {
                bestScore = score;
                best = job;
            }
        }

        return best;
    }

    /// <summary>Check if a pawn meets the requirements for a job.</summary>
    public static bool CanPawnDoJob(Pawn.Pawn pawn, Job job)
    {
        if (string.IsNullOrEmpty(job.RequiredSkill))
            return true;

        float skill = pawn.Data.GetStat(job.RequiredSkill);
        return skill >= job.MinSkillLevel && skill > 0f; // Disabled skills (0) can't work
    }

    /// <summary>
    /// Create a mining job at the given block coordinate.
    /// Call this when the player designates a block for mining.
    /// </summary>
    public Job CreateMineJob(int worldBlockX, int worldBlockZ)
    {
        var job = new Job("Mine", "挖矿")
        {
            TargetBlockCoord = new Vector2I(worldBlockX, worldBlockZ),
            TargetWorldPos = new Vector3(
                worldBlockX * Constants.BlockPixelSize + Constants.BlockPixelSize * 0.5f,
                0f,
                worldBlockZ * Constants.BlockPixelSize + Constants.BlockPixelSize * 0.5f
            ),
            RequiredSkill = "Mining",
            WorkTicks = 300,    // 5 seconds base
            BasePriority = 6,
            XpPerTick = 0.5f,
        };

        AddJob(job);
        return job;
    }

    /// <summary>Create a hauling job.</summary>
    public Job CreateHaulJob(Vector2I from, Vector2I to)
    {
        var job = new Job("Haul", "搬运")
        {
            TargetBlockCoord = from,
            TargetWorldPos = new Vector3(
                from.X * Constants.BlockPixelSize + Constants.BlockPixelSize * 0.5f,
                0f,
                from.Y * Constants.BlockPixelSize + Constants.BlockPixelSize * 0.5f
            ),
            RequiredSkill = "Strength",
            WorkTicks = 120,
            BasePriority = 4,
            XpPerTick = 0.3f,
        };

        AddJob(job);
        return job;
    }

    /// <summary>Create a construction job.</summary>
    public Job CreateConstructJob(int worldBlockX, int worldBlockZ)
    {
        var job = new Job("Construct", "建造")
        {
            TargetBlockCoord = new Vector2I(worldBlockX, worldBlockZ),
            TargetWorldPos = new Vector3(
                worldBlockX * Constants.BlockPixelSize + Constants.BlockPixelSize * 0.5f,
                0f,
                worldBlockZ * Constants.BlockPixelSize + Constants.BlockPixelSize * 0.5f
            ),
            RequiredSkill = "Construction",
            WorkTicks = 480,
            BasePriority = 5,
            XpPerTick = 0.6f,
        };

        AddJob(job);
        return job;
    }

    /// <summary>Get count of jobs by status.</summary>
    public int CountByStatus(JobStatus status) => _jobs.Count(j => j.Status == status);

    /// <summary>All jobs (read-only).</summary>
    public IReadOnlyList<Job> AllJobs => _jobs;

    /// <summary>Cleanup completed/failed jobs older than N ticks.</summary>
    public override void _Process(double delta)
    {
        // Auto-cleanup old completed jobs
        _jobs.RemoveAll(j =>
            j.Status == JobStatus.Completed ||
            j.Status == JobStatus.Failed
        );
    }
}
