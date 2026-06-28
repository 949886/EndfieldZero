using Godot;
using Godot.Collections;

namespace Cherry.Research;

public enum TechnologyEffectType
{
    UnlockBuilding,
    ConstructionSpeedMultiplier,
    ColonyMoveSpeedMultiplier,
}

[GlobalClass]
public partial class TechnologyEffectDef : Resource
{
    [Export] public TechnologyEffectType EffectType { get; set; }
    [Export] public string TargetId { get; set; } = "";
    [Export] public float Value { get; set; }

    public static TechnologyEffectDef UnlockBuilding(string buildingId)
    {
        return new TechnologyEffectDef
        {
            EffectType = TechnologyEffectType.UnlockBuilding,
            TargetId = buildingId,
            Value = 1f,
        };
    }

    public static TechnologyEffectDef Modifier(TechnologyEffectType effectType, float value)
    {
        return new TechnologyEffectDef
        {
            EffectType = effectType,
            Value = value,
        };
    }
}
