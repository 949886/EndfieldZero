using Godot;

namespace EndfieldZero.Jobs;

/// <summary>
/// Represents a single work job that can be claimed and executed by a pawn.
///
/// Job lifecycle: Created → Reserved → InProgress → Completed/Failed/Cancelled
/// </summary>
public class Job
{
    private static int _nextId = 1;

    // --- Identity ---
    public int Id { get; }
    public string JobType { get; set; }          // "Mine", "Construct", "Haul", "Grow", etc.
    public string DisplayName { get; set; }

    // --- Target ---
    public Vector2I TargetBlockCoord { get; set; }   // World block coordinate
    public Vector3 TargetWorldPos { get; set; }       // World 3D position

    // --- Requirements ---
    public string RequiredSkill { get; set; }        // "Mining", "Construction", etc.
    public float MinSkillLevel { get; set; } = 0f;   // Minimum skill to perform
    public int WorkTicks { get; set; } = 300;        // Base ticks to complete (5 seconds)

    // --- Priority ---
    public int BasePriority { get; set; } = 5;       // 1 = low, 10 = high
    public int PlayerPriority { get; set; } = 2;     // Player override: 1-4 (4 = highest)
    public float EffectivePriority => BasePriority * PlayerPriority;

    // --- State ---
    public JobStatus Status { get; set; } = JobStatus.Available;
    public int ReservedByPawnId { get; set; } = -1;
    public int TicksWorked { get; set; }

    // --- XP ---
    public float XpPerTick { get; set; } = 0.5f;     // XP granted per work tick

    public Job(string jobType, string displayName)
    {
        Id = _nextId++;
        JobType = jobType;
        DisplayName = displayName;
    }

    /// <summary>Whether this job can be claimed by a pawn.</summary>
    public bool IsAvailable => Status == JobStatus.Available;

    /// <summary>Reserve this job for a pawn.</summary>
    public void Reserve(int pawnId)
    {
        Status = JobStatus.Reserved;
        ReservedByPawnId = pawnId;
    }

    /// <summary>Start working on this job.</summary>
    public void Start()
    {
        Status = JobStatus.InProgress;
    }

    /// <summary>Mark job as completed.</summary>
    public void Complete()
    {
        Status = JobStatus.Completed;
    }

    /// <summary>Mark job as failed.</summary>
    public void Fail()
    {
        Status = JobStatus.Failed;
        ReservedByPawnId = -1;
    }

    /// <summary>Cancel and release this job.</summary>
    public void Cancel()
    {
        Status = JobStatus.Available;
        ReservedByPawnId = -1;
        TicksWorked = 0;
    }

    /// <summary>Remaining work ticks.</summary>
    public int TicksRemaining => WorkTicks - TicksWorked;

    /// <summary>Progress 0-1.</summary>
    public float Progress => WorkTicks > 0 ? (float)TicksWorked / WorkTicks : 1f;
}

public enum JobStatus
{
    Available,
    Reserved,
    InProgress,
    Completed,
    Failed,
}
