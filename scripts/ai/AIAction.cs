using System.Collections.Generic;
using Godot;

namespace EndfieldZero.AI;

/// <summary>
/// Base class for all AI actions. Each action can score itself based
/// on the pawn's current context and execute behavior when selected.
///
/// Actions define:
///   - QueryVector: what this action "cares about" (need weights, priorities)
///   - CanExecute: whether this action is available
///   - Execute: called each tick while this action is active
///   - IsComplete: whether the action has finished
/// </summary>
public abstract class AIAction
{
    /// <summary>Action display name for debugging.</summary>
    public abstract string Name { get; }

    /// <summary>The pawn this action belongs to.</summary>
    public Pawn.Pawn Owner { get; set; }

    /// <summary>Whether this action is currently running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Generate the Query vector for attention scoring.
    /// Each float represents how much this action "cares about" a context dimension.
    /// Dimensions: [Hunger, Rest, Joy, Comfort, Beauty, Social, JobAvailable, Safety, Idleness]
    /// Values should be 0-1 range.
    /// </summary>
    public abstract float[] GetQueryVector(AIContext context);

    /// <summary>Whether this action can currently be executed.</summary>
    public virtual bool CanExecute(AIContext context) => true;

    /// <summary>Called when this action is selected (starts executing).</summary>
    public virtual void OnStart(AIContext context) { IsRunning = true; }

    /// <summary>Called each tick while this action is active.</summary>
    public abstract void Execute(AIContext context);

    /// <summary>Called when this action is interrupted or replaced.</summary>
    public virtual void OnStop()
    {
        IsRunning = false;
        Owner?.Stop();
    }

    /// <summary>Whether this action has completed its goal.</summary>
    public abstract bool IsComplete(AIContext context);

    /// <summary>
    /// Whether this action can be interrupted by re-evaluation.
    /// Return false to prevent the AI from switching away (e.g., mid-work).
    /// Default: true (always interruptible).
    /// </summary>
    public virtual bool ShouldInterrupt(AIContext context) => true;
}

/// <summary>
/// Context snapshot passed to AI actions for scoring and execution.
/// Encapsulates all environmental information the AI needs.
/// </summary>
public class AIContext
{
    /// <summary>The pawn being evaluated.</summary>
    public Pawn.Pawn Pawn { get; set; }

    /// <summary>Current tick number.</summary>
    public long CurrentTick { get; set; }

    // --- Derived context dimensions (0-1 normalized) ---

    /// <summary>How urgent each need is (1 = critical, 0 = satisfied).</summary>
    public float HungerUrgency => 1f - (Pawn.Needs.Hunger / 100f);
    public float RestUrgency => 1f - (Pawn.Needs.Rest / 100f);
    public float JoyUrgency => 1f - (Pawn.Needs.Joy / 100f);
    public float ComfortUrgency => 1f - (Pawn.Needs.Comfort / 100f);
    public float BeautyUrgency => 1f - (Pawn.Needs.Beauty / 100f);
    public float SocialUrgency => 1f - (Pawn.Needs.Social / 100f);

    /// <summary>Whether there are available jobs (0 or 1).</summary>
    public float JobAvailability { get; set; }

    /// <summary>Safety level (0 = danger, 1 = safe). Currently always safe.</summary>
    public float Safety { get; set; } = 1f;

    /// <summary>How idle the pawn is (1 = doing nothing, 0 = busy).</summary>
    public float Idleness => Pawn.IsMoving ? 0f : 1f;

    /// <summary>Build the Key vector (context state) for attention scoring.</summary>
    public float[] GetKeyVector()
    {
        return new float[]
        {
            HungerUrgency,
            RestUrgency,
            JoyUrgency,
            ComfortUrgency,
            BeautyUrgency,
            SocialUrgency,
            JobAvailability,
            Safety,
            Idleness,
        };
    }

    /// <summary>Dimension count for Q/K vectors.</summary>
    public const int Dimensions = 9;
}
