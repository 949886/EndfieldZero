using System.Collections.Generic;
using Godot;

namespace EndfieldZero.Pawn;

/// <summary>
/// A thought is a temporary or permanent mood modifier.
/// Thoughts have a duration after which they expire.
/// </summary>
public class Thought
{
    public string Id { get; set; }           // e.g. "ate_fine_meal"
    public string Label { get; set; }         // e.g. "吃了好吃的"
    public float MoodOffset { get; set; }    // e.g. +10
    public long ExpiresAtTick { get; set; }   // -1 = permanent until removed
    public bool IsPermanent => ExpiresAtTick < 0;
}

/// <summary>
/// Tracks a pawn's mood via base value + active thoughts.
/// Mood = BaseMood(50) + Σ trait.MoodOffset + Σ thought.MoodOffset
///
/// Thresholds (from PawnData.Will):
///   > 80: Inspired (work speed +20%)
///   60-80: Happy
///   40-60: Normal
///   25-40: Stressed
///   &lt; breakdownThreshold: Mental break risk
/// </summary>
public class MoodTracker
{
    private const float BaseMood = 50f;
    private readonly List<Thought> _thoughts = new();
    private readonly PawnData _data;

    public MoodTracker(PawnData data)
    {
        _data = data;
    }

    /// <summary>All active thoughts (read-only view).</summary>
    public IReadOnlyList<Thought> Thoughts => _thoughts;

    /// <summary>Current mood value (0-100).</summary>
    public float CurrentMood { get; private set; } = BaseMood;

    /// <summary>Add a thought with optional expiry.</summary>
    public void AddThought(string id, string label, float moodOffset, long durationTicks = -1)
    {
        // Remove existing thought with same ID (replace)
        _thoughts.RemoveAll(t => t.Id == id);

        long expiry = durationTicks > 0
            ? (Core.TimeManager.Instance?.CurrentTick ?? 0) + durationTicks
            : -1;

        _thoughts.Add(new Thought
        {
            Id = id,
            Label = label,
            MoodOffset = moodOffset,
            ExpiresAtTick = expiry,
        });

        RecalculateMood();
    }

    /// <summary>Remove a specific thought by ID.</summary>
    public void RemoveThought(string id)
    {
        _thoughts.RemoveAll(t => t.Id == id);
        RecalculateMood();
    }

    /// <summary>Called every tick — expires old thoughts and recalculates mood.</summary>
    public void Tick(long currentTick)
    {
        int removed = _thoughts.RemoveAll(t => !t.IsPermanent && t.ExpiresAtTick <= currentTick);
        if (removed > 0 || _thoughts.Count > 0)
            RecalculateMood();
    }

    /// <summary>Check if pawn is at risk of mental break.</summary>
    public bool IsBreakdownRisk()
    {
        return CurrentMood < _data.GetBreakdownThreshold();
    }

    /// <summary>Check if pawn is inspired (mood > 80).</summary>
    public bool IsInspired() => CurrentMood > 80f;

    /// <summary>Get work speed modifier based on mood.</summary>
    public float GetWorkSpeedModifier()
    {
        if (CurrentMood > 80f) return 1.2f;     // Inspired
        if (CurrentMood > 60f) return 1.0f;     // Happy/Normal
        if (CurrentMood > 40f) return 0.9f;     // Slightly stressed
        if (CurrentMood > 25f) return 0.75f;    // Stressed
        return 0.5f;                             // Near breakdown
    }

    private void RecalculateMood()
    {
        float mood = BaseMood;

        // Trait permanent mood offsets
        foreach (var trait in _data.Traits)
        {
            mood += trait.MoodOffset;
        }

        // Active thoughts
        foreach (var thought in _thoughts)
        {
            mood += thought.MoodOffset;
        }

        CurrentMood = Mathf.Clamp(mood, 0f, 100f);
    }
}
