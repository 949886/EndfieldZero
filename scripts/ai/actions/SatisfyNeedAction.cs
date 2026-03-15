using Godot;

namespace EndfieldZero.AI.Actions;

/// <summary>
/// SatisfyNeedAction — attempts to satisfy the pawn's most critical need.
/// Currently a simplified implementation that directly replenishes needs
/// (will be expanded with actual objects like food items, beds, etc.)
///
/// Q vector: directly mirrors need urgencies with high weights.
/// This makes it score very high when needs are low.
/// </summary>
public class SatisfyNeedAction : AIAction
{
    public override string Name => "SatisfyNeed";

    private string _targetNeed;
    private int _satisfyTicksRemaining;
    private bool _isComplete;

    // How many ticks to "satisfy" a need (simulates eating, resting, etc.)
    private const int HungerSatisfyTicks = 180;    // 3 seconds
    private const int RestSatisfyTicks = 600;       // 10 seconds
    private const int JoySatisfyTicks = 300;        // 5 seconds
    private const int SocialSatisfyTicks = 240;     // 4 seconds
    private const int DefaultSatisfyTicks = 180;

    public override float[] GetQueryVector(AIContext context)
    {
        // Heavily weight need urgencies — the more urgent, the higher the score
        return new float[]
        {
            context.HungerUrgency * 1.5f,   // Hunger is highest priority
            context.RestUrgency * 1.4f,      // Rest is very important
            context.JoyUrgency * 0.8f,       // Joy matters but less urgent
            context.ComfortUrgency * 0.5f,
            context.BeautyUrgency * 0.3f,
            context.SocialUrgency * 0.6f,
            0.0f,                             // Jobs — irrelevant
            0.0f,                             // Safety
            0.1f,                             // Idleness — slight preference when idle
        };
    }

    public override bool CanExecute(AIContext context)
    {
        // Only execute when at least one need is below 50
        var pawn = context.Pawn;
        return pawn.Needs.Hunger < 50f || pawn.Needs.Rest < 50f
            || pawn.Needs.Joy < 50f || pawn.Needs.Social < 50f
            || pawn.Needs.Comfort < 50f || pawn.Needs.Beauty < 50f;
    }

    public override void OnStart(AIContext context)
    {
        base.OnStart(context);
        _isComplete = false;

        // Pick the most urgent need
        var (name, _) = context.Pawn.Needs.GetMostUrgent();
        _targetNeed = name;

        _satisfyTicksRemaining = _targetNeed switch
        {
            "Hunger" => HungerSatisfyTicks,
            "Rest" => RestSatisfyTicks,
            "Joy" => JoySatisfyTicks,
            "Social" => SocialSatisfyTicks,
            _ => DefaultSatisfyTicks,
        };

        // Stop moving while satisfying need
        Owner.Stop();

        GD.Print($"[AI] {Owner.Data.PawnName} satisfying {_targetNeed} ({context.Pawn.Needs.GetByName(_targetNeed):F0}/100)");
    }

    public override void Execute(AIContext context)
    {
        if (_isComplete) return;

        _satisfyTicksRemaining--;

        // Gradually replenish the need
        float replenishRate = GetReplenishRate(_targetNeed);
        float current = Owner.Needs.GetByName(_targetNeed);
        Owner.Needs.SetByName(_targetNeed, current + replenishRate);

        // Add mood thought when starting to eat/rest
        if (_satisfyTicksRemaining == GetTotalTicks(_targetNeed) - 1)
        {
            string thoughtId = $"satisfying_{_targetNeed.ToLower()}";
            string label = _targetNeed switch
            {
                "Hunger" => "正在进食",
                "Rest" => "正在休息",
                "Joy" => "正在娱乐",
                "Social" => "正在社交",
                _ => "正在恢复",
            };
            Owner.Mood.AddThought(thoughtId, label, 3f, Core.TimeManager.TicksPerHour * 2);
        }

        // Complete when ticks run out or need is satisfied
        if (_satisfyTicksRemaining <= 0 || Owner.Needs.GetByName(_targetNeed) >= 90f)
        {
            _isComplete = true;

            // Positive mood on completion
            if (Owner.Needs.GetByName(_targetNeed) >= 80f)
            {
                string thoughtId = $"satisfied_{_targetNeed.ToLower()}";
                string label = _targetNeed switch
                {
                    "Hunger" => "吃饱了",
                    "Rest" => "休息好了",
                    "Joy" => "心情不错",
                    "Social" => "愉快的交流",
                    _ => "感觉不错",
                };
                Owner.Mood.AddThought(thoughtId, label, 8f, Core.TimeManager.TicksPerHour * 4);
            }
        }
    }

    public override bool IsComplete(AIContext context) => _isComplete;

    private float GetReplenishRate(string need)
    {
        return need switch
        {
            "Hunger" => 0.4f,   // ~45 ticks to fill 0→100 portion during eating
            "Rest" => 0.15f,    // Slower recovery
            "Joy" => 0.25f,
            "Social" => 0.3f,
            "Comfort" => 0.2f,
            "Beauty" => 0.2f,
            _ => 0.2f,
        };
    }

    private int GetTotalTicks(string need)
    {
        return need switch
        {
            "Hunger" => HungerSatisfyTicks,
            "Rest" => RestSatisfyTicks,
            "Joy" => JoySatisfyTicks,
            "Social" => SocialSatisfyTicks,
            _ => DefaultSatisfyTicks,
        };
    }
}
