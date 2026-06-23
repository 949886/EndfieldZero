using System;
using System.Collections.Generic;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Combat;

/// <summary>
/// Health component for a pawn. Tracks HP, wounds, pain.
/// Attach to Pawn via composition (not inheritance).
/// </summary>
public class HealthComponent
{
    private readonly Pawn.PawnData _data;
    private readonly Action<string> _onDeath;

    public float MaxHp { get; private set; }
    public float CurrentHp { get; private set; }
    public bool IsDead => CurrentHp <= 0f;
    public float HpPercent => MaxHp > 0 ? CurrentHp / MaxHp : 0f;

    public List<Wound> Wounds { get; } = new();

    public HealthComponent(Pawn.PawnData data, Action<string> onDeath)
    {
        _data = data;
        _onDeath = onDeath;
        // Base 80 + Strength × 4 → range ~92-160
        if (data.IsHostile)
        {
            MaxHp = Settings.HostileBaseHp + data.GetStat("Strength") * Settings.HostileHpPerStrength;
        }
        else
        {
            MaxHp = 80f + data.GetStat("Strength") * 4f;
        }
        CurrentHp = MaxHp;
    }

    /// <summary>Apply damage. Returns actual damage dealt after dodge/armor.</summary>
    public float TakeDamage(float rawDamage, int sourceId = -1, string bodyPart = null)
    {
        if (IsDead) return 0f;

        float actual = Mathf.Max(1f, rawDamage);
        CurrentHp = Mathf.Max(0f, CurrentHp - actual);

        // Create wound
        string part = bodyPart ?? RandomBodyPart();
        var severity = actual > 15f ? WoundSeverity.Major
                     : actual > 8f  ? WoundSeverity.Minor
                     : WoundSeverity.Scratch;
        Wounds.Add(new Wound(part, severity, actual));

        EventBus.FirePawnDamaged(_data.Id, actual, sourceId);

        if (IsDead)
        {
            _onDeath?.Invoke("combat");
        }

        return actual;
    }

    /// <summary>Heal HP.</summary>
    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
    }

    /// <summary>Slow natural healing per tick (only when not in combat).</summary>
    public void TickHeal()
    {
        if (IsDead || CurrentHp >= MaxHp) return;
        // 0.01 HP per tick → ~0.6 HP/sec → full heal in ~2-3 minutes
        Heal(0.01f);

        // Slowly remove minor wounds
        for (int i = Wounds.Count - 1; i >= 0; i--)
        {
            if (Wounds[i].Severity == WoundSeverity.Scratch)
            {
                Wounds[i].HealProgress += 0.002f;
                if (Wounds[i].HealProgress >= 1f)
                    Wounds.RemoveAt(i);
            }
        }
    }

    /// <summary>Total pain level 0-1. Affects movement speed and mood.</summary>
    public float GetPainLevel()
    {
        float pain = 0f;
        foreach (var w in Wounds)
        {
            pain += w.Severity switch
            {
                WoundSeverity.Scratch => 0.02f,
                WoundSeverity.Minor => 0.08f,
                WoundSeverity.Major => 0.2f,
                WoundSeverity.Permanent => 0.15f,
                _ => 0f,
            };
        }
        return Mathf.Clamp(pain, 0f, 1f);
    }

    private static string RandomBodyPart()
    {
        string[] parts = { "头部", "躯干", "左臂", "右臂", "左腿", "右腿" };
        return parts[new Random().Next(parts.Length)];
    }
}

public enum WoundSeverity { Scratch, Minor, Major, Permanent }

public class Wound
{
    public string BodyPart { get; }
    public WoundSeverity Severity { get; }
    public float Damage { get; }
    public float HealProgress { get; set; }

    public Wound(string bodyPart, WoundSeverity severity, float damage)
    {
        BodyPart = bodyPart;
        Severity = severity;
        Damage = damage;
    }
}
