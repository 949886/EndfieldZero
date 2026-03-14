using Godot;
using Godot.Collections;

namespace EndfieldZero.Pawn;

/// <summary>
/// Trait Resource — a personality trait that modifies a pawn's stats and needs.
/// Traits are data-driven: create instances in the editor or load from definitions.
///
/// Example traits:
///   "勤劳" Hardworking: Mining +2, Construction +1, RestDecay +20%
///   "懒惰" Lazy: all work skills -1, RestDecay -30%
///   "夜猫子" NightOwl: RestDecay -20% at night, +30% during day
///   "和平主义" Pacifist: disables Shooting, Strength; Social +3
/// </summary>
[GlobalClass]
public partial class Trait : Resource
{
    /// <summary>Unique identifier. e.g. "hardWorking".</summary>
    [Export] public string Id { get; set; } = "";

    /// <summary>Display name. e.g. "勤劳".</summary>
    [Export] public string DisplayName { get; set; } = "";

    /// <summary>Description text shown to player.</summary>
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";

    /// <summary>
    /// Stat modifications: key = stat field name, value = additive modifier.
    /// e.g. { "Mining": 2.0, "Agility": -1.0 }
    /// </summary>
    [Export] public Dictionary<string, float> StatModifiers { get; set; } = new();

    /// <summary>
    /// Need decay rate multipliers: key = need name, value = multiplier.
    /// e.g. { "Rest": 0.7 } means 30% slower rest decay.
    /// </summary>
    [Export] public Dictionary<string, float> NeedModifiers { get; set; } = new();

    /// <summary>
    /// Skills that are disabled (locked at 0) by this trait.
    /// e.g. ["Shooting", "Strength"] for Pacifist.
    /// </summary>
    [Export] public string[] DisabledSkills { get; set; } = System.Array.Empty<string>();

    /// <summary>Mood offset from having this trait (permanent). e.g. +5 for Optimist.</summary>
    [Export] public float MoodOffset { get; set; } = 0f;
}
