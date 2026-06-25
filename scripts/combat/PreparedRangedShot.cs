using Godot;

namespace EndfieldZero.Combat;

/// <summary>
/// Captures a ranged attack outcome at fire time and resolves it on impact.
/// </summary>
public sealed class PreparedRangedShot
{
    public PreparedRangedShot(
        Pawn.Pawn attacker,
        Pawn.EnemyPawn target,
        WeaponDef weapon,
        bool isHit,
        bool isDodged,
        bool isCrit,
        float damage,
        Vector3 targetPoint,
        Vector3 impactPoint)
    {
        Attacker = attacker;
        Target = target;
        Weapon = weapon;
        IsHit = isHit;
        IsDodged = isDodged;
        IsCrit = isCrit;
        Damage = damage;
        TargetPoint = targetPoint;
        ImpactPoint = impactPoint;
    }

    public Pawn.Pawn Attacker { get; }
    public Pawn.EnemyPawn Target { get; }
    public WeaponDef Weapon { get; }
    public bool IsHit { get; }
    public bool IsDodged { get; }
    public bool IsCrit { get; }
    public float Damage { get; }
    public Vector3 TargetPoint { get; }
    public Vector3 ImpactPoint { get; }

    private bool _resolved;

    public bool TryApply(out float actualDamage)
    {
        actualDamage = 0f;
        if (_resolved)
            return false;

        _resolved = true;

        if (Attacker?.Data == null || Target?.Data == null || Weapon == null)
            return false;

        if (!GodotObject.IsInstanceValid(Target) || Target.Health?.IsDead == true)
            return false;

        if (!IsHit)
        {
            string reason = IsDodged ? "dodged" : "missed";
            GD.Print($"[Combat] {Attacker.Data.PawnName} {reason} {Target.Data.PawnName}");
            return false;
        }

        actualDamage = Target.Health.TakeDamage(Damage, Attacker.Data.Id);
        string critStr = IsCrit ? " crit!" : "";
        GD.Print($"[Combat] {Attacker.Data.PawnName} hit {Target.Data.PawnName}: {actualDamage:F0} dmg ({Weapon.DisplayName}){critStr}");
        return true;
    }
}
