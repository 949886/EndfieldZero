using System.Collections.Generic;
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
        //                            id           name    ranged  dmg  range  cd   acc
        reg.Register(new WeaponDef("fist",         "拳头",  false,  5f,  1f,   60,  1.0f));
        reg.Register(new WeaponDef("knife",        "刀",    false, 10f,  1f,   45,  1.0f));
        reg.Register(new WeaponDef("spear",        "矛",    false, 14f,  1.5f, 60,  1.0f));
        reg.Register(new WeaponDef("hammer",       "锤",    false, 18f,  1f,   90,  0.9f));
        reg.Register(new WeaponDef("bow",          "弓",    true,  12f, 15f,  120,  0.85f));
        reg.Register(new WeaponDef("crossbow",     "弩",    true,  16f, 20f,  180,  0.9f));
        return reg;
    }
}
