using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace EndfieldZero.Pawn;

/// <summary>Gender enum for pawn identity.</summary>
public enum Gender { Male, Female, Other }

/// <summary>
/// Core pawn data stored as a Godot Resource. Contains identity, stats, needs, and traits.
/// Can be serialized/deserialized by Godot's resource system for save/load.
/// </summary>
[GlobalClass]
public partial class PawnData : Resource
{
    // --- Identity ---
    [Export] public int Id { get; set; }
    [Export] public string PawnName { get; set; } = "Colonist";
    [Export] public string Nickname { get; set; } = "";
    [Export] public int Age { get; set; } = 25;
    [Export] public Gender Gender { get; set; } = Gender.Male;
    [Export(PropertyHint.MultilineText)] public string BackgroundStory { get; set; } = "";

    // --- Traits ---
    [Export] public Godot.Collections.Array<Trait> Traits { get; set; } = new();

    // --- Abilities (inner data class) ---
    // 12 stats: 8 work skills + 4 core attributes. Each 0-20 with XP.

    // Work skills
    [ExportGroup("Work Skills")]
    [Export(PropertyHint.Range, "0,20,0.1")] public float Mining { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Construction { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Growing { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Cooking { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Crafting { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Medical { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Social { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Artistic { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float Shooting { get; set; } = 5f;

    // Core attributes
    [ExportGroup("Core Attributes")]
    [Export(PropertyHint.Range, "0,20,0.1")] public float Strength { get; set; } = 5f;   // 近战伤害 + 搬运
    [Export(PropertyHint.Range, "0,20,0.1")] public float Intellect { get; set; } = 5f;  // 研究速度 + 法术伤害
    [Export(PropertyHint.Range, "0,20,0.1")] public float Agility { get; set; } = 5f;    // 移动速度 + 闪避率
    [Export(PropertyHint.Range, "0,20,0.1")] public float Will { get; set; } = 5f;       // 心情稳定 + 崩溃阈值

    // --- XP tracking (not exported, runtime only) ---
    // Key = stat name, Value = accumulated XP
    private readonly System.Collections.Generic.Dictionary<string, float> _experience = new();

    private static readonly string[] AllStatNames = {
        "Mining", "Construction", "Growing", "Cooking", "Crafting",
        "Medical", "Social", "Artistic", "Shooting",
        "Strength", "Intellect", "Agility", "Will"
    };

    /// <summary>Get a stat value by name, with trait modifiers applied.</summary>
    public float GetStat(string name)
    {
        float baseVal = GetBaseStat(name);

        // Apply trait modifiers
        foreach (var trait in Traits)
        {
            // Check if disabled
            foreach (var disabled in trait.DisabledSkills)
            {
                if (disabled == name) return 0f;
            }

            if (trait.StatModifiers.TryGetValue(name, out float mod))
                baseVal += mod;
        }

        return Mathf.Clamp(baseVal, 0f, 20f);
    }

    /// <summary>Get base stat without trait modifiers.</summary>
    public float GetBaseStat(string name)
    {
        return name switch
        {
            "Mining" => Mining, "Construction" => Construction,
            "Growing" => Growing, "Cooking" => Cooking,
            "Crafting" => Crafting, "Medical" => Medical,
            "Social" => Social, "Artistic" => Artistic,
            "Shooting" => Shooting,
            "Strength" => Strength, "Intellect" => Intellect,
            "Agility" => Agility, "Will" => Will,
            _ => 0f,
        };
    }

    /// <summary>Add XP to a stat. Auto-levels up when XP threshold is reached.</summary>
    public void AddExperience(string statName, float amount)
    {
        if (!_experience.ContainsKey(statName))
            _experience[statName] = 0f;

        _experience[statName] += amount;

        // XP required = current level × 100 (e.g. level 5 → 500 XP to next)
        float currentLevel = GetBaseStat(statName);
        float xpRequired = currentLevel * 100f;

        if (_experience[statName] >= xpRequired && currentLevel < 20f)
        {
            _experience[statName] -= xpRequired;
            SetBaseStat(statName, currentLevel + 1f);
        }
    }

    /// <summary>Get movement speed multiplier based on Agility stat.</summary>
    public float GetMoveSpeedMultiplier()
    {
        // Agility 5 = 1.0×, every point ±5%
        return 1f + (GetStat("Agility") - 5f) * 0.05f;
    }

    /// <summary>Get carry capacity based on Strength stat.</summary>
    public float GetCarryCapacity()
    {
        return 50f + GetStat("Strength") * 5f;  // 50 base + 5 per Strength
    }

    /// <summary>Get mental break threshold based on Will stat.</summary>
    public float GetBreakdownThreshold()
    {
        // Will 5 = 25 mood threshold, higher Will = lower threshold (more resilient)
        return Mathf.Max(5f, 25f - GetStat("Will") * 1.5f);
    }

    private void SetBaseStat(string name, float value)
    {
        value = Mathf.Clamp(value, 0f, 20f);
        switch (name)
        {
            case "Mining": Mining = value; break;
            case "Construction": Construction = value; break;
            case "Growing": Growing = value; break;
            case "Cooking": Cooking = value; break;
            case "Crafting": Crafting = value; break;
            case "Medical": Medical = value; break;
            case "Social": Social = value; break;
            case "Artistic": Artistic = value; break;
            case "Shooting": Shooting = value; break;
            case "Strength": Strength = value; break;
            case "Intellect": Intellect = value; break;
            case "Agility": Agility = value; break;
            case "Will": Will = value; break;
        }
    }

    /// <summary>Apply trait need modifiers to a Needs instance.</summary>
    public void ApplyTraitNeedModifiers(Needs needs)
    {
        foreach (var trait in Traits)
        {
            foreach (var kv in trait.NeedModifiers)
            {
                string needName = kv.Key;
                float multiplier = kv.Value;

                // Modify decay rate
                switch (needName)
                {
                    case "Hunger":  needs.HungerDecay  *= multiplier; break;
                    case "Rest":    needs.RestDecay     *= multiplier; break;
                    case "Joy":     needs.JoyDecay      *= multiplier; break;
                    case "Comfort": needs.ComfortDecay  *= multiplier; break;
                    case "Beauty":  needs.BeautyDecay   *= multiplier; break;
                    case "Social":  needs.SocialDecay   *= multiplier; break;
                }
            }
        }
    }
}
