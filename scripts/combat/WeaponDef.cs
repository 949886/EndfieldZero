using System.Collections.Generic;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Combat;

/// <summary>
/// Weapon static definition.
///
/// Melee: Fist(default), Knife, Spear, Hammer
/// Ranged: Bow, Crossbow
/// </summary>
public class WeaponDef
{
    public string Id { get; }
    public string DisplayName { get; }
    public bool IsRanged { get; }
    public float BaseDamage { get; }
    public float Range { get; }          // in blocks
    public int CooldownTicks { get; }
    public float AccuracyMod { get; }    // 1.0 = base

    public WeaponDef(string id, string displayName, bool isRanged,
        float baseDamage, float range, int cooldownTicks, float accuracyMod = 1f)
    {
        Id = id;
        DisplayName = displayName;
        IsRanged = isRanged;
        BaseDamage = baseDamage;
        Range = range;
        CooldownTicks = cooldownTicks;
        AccuracyMod = accuracyMod;
    }
}

/// <summary>
/// Registry of all weapon definitions.
/// </summary>
public sealed class WeaponRegistry
{
    private readonly Dictionary<string, WeaponDef> _defs = new();
    private static WeaponRegistry _instance;
    public static WeaponRegistry Instance => _instance ??= CreateDefault();

    public WeaponDef GetDef(string id) => _defs.GetValueOrDefault(id);
    public IEnumerable<WeaponDef> AllDefs => _defs.Values;

    private void Register(WeaponDef def) => _defs[def.Id] = def;

    private static WeaponRegistry CreateDefault()
    {
        var reg = new WeaponRegistry();
        reg.Register(new WeaponDef("fist", "Fist", false,
            Settings.FistBaseDamage, Settings.FistRange, Settings.FistCooldownTicks, Settings.FistAccuracyMod));
        reg.Register(new WeaponDef("knife", "Knife", false,
            Settings.KnifeBaseDamage, Settings.KnifeRange, Settings.KnifeCooldownTicks, Settings.KnifeAccuracyMod));
        reg.Register(new WeaponDef("spear", "Spear", false,
            Settings.SpearBaseDamage, Settings.SpearRange, Settings.SpearCooldownTicks, Settings.SpearAccuracyMod));
        reg.Register(new WeaponDef("hammer", "Hammer", false,
            Settings.HammerBaseDamage, Settings.HammerRange, Settings.HammerCooldownTicks, Settings.HammerAccuracyMod));
        reg.Register(new WeaponDef("bow", "Bow", true,
            Settings.BowBaseDamage, Settings.BowRange, Settings.BowCooldownTicks, Settings.BowAccuracyMod));
        reg.Register(new WeaponDef("crossbow", "Crossbow", true,
            Settings.CrossbowBaseDamage, Settings.CrossbowRange, Settings.CrossbowCooldownTicks, Settings.CrossbowAccuracyMod));
        reg.Register(new WeaponDef("rifle", "Rifle", true,
            Settings.RifleBaseDamage, Settings.RifleRange, Settings.RifleCooldownTicks, Settings.RifleAccuracyMod));
        return reg;
    }
}
