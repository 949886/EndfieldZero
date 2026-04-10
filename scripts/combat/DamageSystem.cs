using System;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Combat;

/// <summary>
/// Static damage calculation utilities.
/// Handles melee/ranged damage, dodge, crit, and environment damage.
/// </summary>
public static class DamageSystem
{
    private static readonly Random Rng = new();

    /// <summary>
    /// Calculate and apply damage from attacker to target.
    /// Returns actual damage dealt (0 if dodged).
    /// </summary>
    public static float Attack(Pawn.Pawn attacker, Pawn.Pawn target)
    {
        if (attacker?.Health == null || target?.Health == null) return 0f;
        if (target.Health.IsDead) return 0f;

        var weapon = GetWeapon(attacker);
        float baseDmg = weapon.BaseDamage;

        // Stat scaling
        float statBonus = weapon.IsRanged
            ? attacker.Data.GetStat("Shooting") / 10f
            : attacker.Data.GetStat("Strength") / 10f;
        float damage = baseDmg * (1f + statBonus);

        // Accuracy (ranged distance falloff)
        if (weapon.IsRanged)
        {
            float dist = attacker.GlobalPosition.DistanceTo(target.GlobalPosition)
                         / Settings.BlockPixelSize;
            float falloff = Mathf.Clamp(1f - dist / (weapon.Range * 1.5f), 0.3f, 1f);
            damage *= falloff * weapon.AccuracyMod;

            // Miss chance
            float hitChance = 0.7f + attacker.Data.GetStat("Shooting") * 0.02f;
            hitChance *= falloff;
            if (Rng.NextDouble() > hitChance)
            {
                GD.Print($"[Combat] {attacker.Data.PawnName} missed {target.Data.PawnName}");
                return 0f;
            }
        }

        // Dodge check: Agility × 3%
        float dodgeChance = target.Data.GetStat("Agility") * 0.03f;
        if (Rng.NextDouble() < dodgeChance)
        {
            GD.Print($"[Combat] {target.Data.PawnName} dodged attack from {attacker.Data.PawnName}");
            return 0f;
        }

        // Crit: 5% + Agility × 1%
        float critChance = 0.05f + attacker.Data.GetStat("Agility") * 0.01f;
        bool isCrit = Rng.NextDouble() < critChance;
        if (isCrit) damage *= 2f;

        // Apply
        float actual = target.Health.TakeDamage(damage, attacker.Data.Id);

        string critStr = isCrit ? " 暴击!" : "";
        GD.Print($"[Combat] {attacker.Data.PawnName} → {target.Data.PawnName}: {actual:F0} dmg ({weapon.DisplayName}){critStr}");
        return actual;
    }

    /// <summary>Get equipped weapon or default fist.</summary>
    public static WeaponDef GetWeapon(Pawn.Pawn pawn)
    {
        if (!string.IsNullOrEmpty(pawn.Data.EquippedWeaponId))
        {
            var w = WeaponRegistry.Instance.GetDef(pawn.Data.EquippedWeaponId);
            if (w != null) return w;
        }
        return WeaponRegistry.Instance.GetDef("fist");
    }

    /// <summary>Check if target is within weapon range.</summary>
    public static bool IsInRange(Pawn.Pawn attacker, Pawn.Pawn target)
    {
        var weapon = GetWeapon(attacker);
        float dist = attacker.GlobalPosition.DistanceTo(target.GlobalPosition)
                     / Settings.BlockPixelSize;
        return dist <= weapon.Range;
    }

    /// <summary>Get effective weapon range in world units.</summary>
    public static float GetRangeWorld(Pawn.Pawn pawn)
    {
        var weapon = GetWeapon(pawn);
        return weapon.Range * Settings.BlockPixelSize;
    }
}
