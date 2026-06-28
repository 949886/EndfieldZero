using System;
using Cherry.Core;
using Godot;

namespace Cherry.Combat;

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

    /// <summary>Colonist attacks an EnemyPawn.</summary>
    public static float Attack(Pawn.Pawn attacker, Pawn.EnemyPawn target)
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

        // Dodge check: Enemy Agility × 3%
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

    /// <summary>
    /// Precompute a ranged shot so visuals can travel before the hit resolves.
    /// </summary>
    public static PreparedRangedShot PrepareRangedAttack(Pawn.Pawn attacker, Pawn.EnemyPawn target)
    {
        if (attacker?.Health == null || target?.Health == null)
            return null;

        if (target.Health.IsDead)
            return null;

        var weapon = GetWeapon(attacker);
        if (weapon == null || !weapon.IsRanged)
            return null;

        float damage = weapon.BaseDamage;
        float statBonus = attacker.Data.GetStat("Shooting") / 10f;
        damage *= 1f + statBonus;

        float dist = attacker.GlobalPosition.DistanceTo(target.GlobalPosition) / Settings.BlockPixelSize;
        float falloff = Mathf.Clamp(1f - dist / (weapon.Range * 1.5f), 0.3f, 1f);
        damage *= falloff * weapon.AccuracyMod;

        float hitChance = 0.7f + attacker.Data.GetStat("Shooting") * 0.02f;
        hitChance *= falloff;
        bool missed = Rng.NextDouble() > hitChance;

        float dodgeChance = target.Data.GetStat("Agility") * 0.03f;
        bool dodged = !missed && Rng.NextDouble() < dodgeChance;

        float critChance = 0.05f + attacker.Data.GetStat("Agility") * 0.01f;
        bool isCrit = !missed && !dodged && Rng.NextDouble() < critChance;
        if (isCrit)
            damage *= 2f;

        Vector3 targetPoint = GetEnemyAimPoint(target);
        Vector3 impactPoint = missed || dodged
            ? targetPoint + GetMissOffset(attacker.GlobalPosition, target.GlobalPosition)
            : targetPoint;

        return new PreparedRangedShot(
            attacker,
            target,
            weapon,
            !missed && !dodged,
            dodged,
            isCrit,
            damage,
            targetPoint,
            impactPoint);
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

    /// <summary>Check if target EnemyPawn is within weapon range.</summary>
    public static bool IsInRange(Pawn.Pawn attacker, Pawn.EnemyPawn target)
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

    // --- EnemyPawn overloads ---

    /// <summary>Get weapon from PawnData directly (for EnemyPawn).</summary>
    public static WeaponDef GetWeapon(Pawn.PawnData data)
    {
        if (!string.IsNullOrEmpty(data.EquippedWeaponId))
        {
            var w = WeaponRegistry.Instance.GetDef(data.EquippedWeaponId);
            if (w != null) return w;
        }
        return WeaponRegistry.Instance.GetDef("fist");
    }

    /// <summary>Check if EnemyPawn is within weapon range of a colonist Pawn.</summary>
    public static bool IsInRange(Pawn.EnemyPawn attacker, Pawn.Pawn target)
    {
        var weapon = GetWeapon(attacker.Data);
        float dist = attacker.GlobalPosition.DistanceTo(target.GlobalPosition)
                     / Settings.BlockPixelSize;
        return dist <= weapon.Range * Settings.HostileRangeMultiplier;
    }

    /// <summary>EnemyPawn attacks a colonist Pawn.</summary>
    public static float AttackEnemy(Pawn.EnemyPawn attacker, Pawn.Pawn target)
    {
        if (target?.Health == null) return 0f;
        if (target.Health.IsDead) return 0f;

        var weapon = GetWeapon(attacker.Data);
        float baseDmg = weapon.BaseDamage;

        // Stat scaling
        float statBonus = weapon.IsRanged
            ? attacker.Data.GetStat("Shooting") / 10f
            : attacker.Data.GetStat("Strength") / 10f;
        float damage = baseDmg * (1f + statBonus) * Settings.HostileDamageMultiplier;

        // Accuracy (ranged distance falloff)
        if (weapon.IsRanged)
        {
            float dist = attacker.GlobalPosition.DistanceTo(target.GlobalPosition)
                         / Settings.BlockPixelSize;
            float effectiveRange = weapon.Range * Settings.HostileRangeMultiplier;
            float falloff = Mathf.Clamp(1f - dist / (effectiveRange * 1.5f), 0.3f, 1f);
            damage *= falloff * weapon.AccuracyMod;

            float hitChance = 0.7f + attacker.Data.GetStat("Shooting") * 0.02f;
            hitChance *= falloff;
            if (Rng.NextDouble() > hitChance)
            {
                GD.Print($"[Combat] {attacker.Data.PawnName} missed {target.Data.PawnName}");
                return 0f;
            }
        }

        // Dodge
        float dodgeChance = target.Data.GetStat("Agility") * 0.03f;
        if (Rng.NextDouble() < dodgeChance)
        {
            GD.Print($"[Combat] {target.Data.PawnName} dodged attack from {attacker.Data.PawnName}");
            return 0f;
        }

        // Crit
        float critChance = 0.05f + attacker.Data.GetStat("Agility") * 0.01f;
        bool isCrit = Rng.NextDouble() < critChance;
        if (isCrit) damage *= 2f;

        float actual = target.Health.TakeDamage(damage, attacker.Data.Id);

        string critStr = isCrit ? " 暴击!" : "";
        GD.Print($"[Combat] {attacker.Data.PawnName} → {target.Data.PawnName}: {actual:F0} dmg ({weapon.DisplayName}){critStr}");
        return actual;
    }
    private static Vector3 GetEnemyAimPoint(Pawn.EnemyPawn target)
    {
        return target?.GetCombatAimPoint() ?? Vector3.Zero;
    }

    private static Vector3 GetMissOffset(Vector3 attackerPosition, Vector3 targetPosition)
    {
        Vector3 forward = (targetPosition - attackerPosition).Normalized();
        if (forward.LengthSquared() <= 0.0001f)
            forward = Vector3.Forward;

        Vector3 right = Vector3.Up.Cross(forward).Normalized();
        if (right.LengthSquared() <= 0.0001f)
            right = Vector3.Right;

        float side = Mathf.Lerp(-0.6f, 0.6f, (float)Rng.NextDouble());
        float depth = Mathf.Lerp(-0.25f, 0.35f, (float)Rng.NextDouble());
        float lift = Mathf.Lerp(-0.15f, 0.25f, (float)Rng.NextDouble());
        return right * side + forward * depth + Vector3.Up * lift;
    }
}
