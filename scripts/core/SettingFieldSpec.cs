using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace Cherry.Core;

public enum AdvancedSettingsTab
{
    World,
    Selection,
    Hostiles,
    Raids,
    Weapons,
    Environment,
}

public sealed class SettingFieldSpec
{
    private readonly PropertyInfo _preferencesProperty;
    private readonly PropertyInfo _gameSettingsProperty;

    public SettingFieldSpec(
        AdvancedSettingsTab tab,
        string label,
        string sectionSuffix,
        string preferencePropertyName,
        string gameSettingsPropertyName,
        double minValue,
        double maxValue,
        double step,
        bool isIntegral,
        int decimalPlaces = 2)
    {
        Tab = tab;
        Label = label;
        SectionSuffix = sectionSuffix;
        PreferencePropertyName = preferencePropertyName;
        GameSettingsPropertyName = gameSettingsPropertyName;
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step;
        IsIntegral = isIntegral;
        DecimalPlaces = decimalPlaces;

        _preferencesProperty = typeof(PlayerPreferences).GetProperty(preferencePropertyName)
            ?? throw new InvalidOperationException($"Unknown PlayerPreferences property: {preferencePropertyName}");
        _gameSettingsProperty = typeof(GameSettings).GetProperty(gameSettingsPropertyName)
            ?? throw new InvalidOperationException($"Unknown GameSettings property: {gameSettingsPropertyName}");
    }

    public AdvancedSettingsTab Tab { get; }
    public string Label { get; }
    public string SectionSuffix { get; }
    public string PreferencePropertyName { get; }
    public string GameSettingsPropertyName { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double Step { get; }
    public bool IsIntegral { get; }
    public int DecimalPlaces { get; }

    public double GetPreferenceValue(PlayerPreferences preferences)
    {
        return Convert.ToDouble(_preferencesProperty.GetValue(preferences));
    }

    public void SetPreferenceValue(PlayerPreferences preferences, double value)
    {
        double clamped = Mathf.Clamp((float)value, (float)MinValue, (float)MaxValue);
        object boxedValue = IsIntegral
            ? (object)Mathf.RoundToInt((float)clamped)
            : (float)clamped;
        _preferencesProperty.SetValue(preferences, boxedValue);
    }

    public void ApplyTo(GameSettings settings, PlayerPreferences preferences)
    {
        object value = _preferencesProperty.GetValue(preferences);
        _gameSettingsProperty.SetValue(settings, value);
    }

    public string FormatValue(PlayerPreferences preferences)
    {
        double value = GetPreferenceValue(preferences);
        return IsIntegral ? $"{Mathf.RoundToInt((float)value)}" : value.ToString($"F{DecimalPlaces}");
    }
}

public static class SettingsFieldRegistry
{
    public static IReadOnlyList<SettingFieldSpec> AdvancedFields { get; } = new[]
    {
        new SettingFieldSpec(AdvancedSettingsTab.World, "Tick Rate", "world", nameof(PlayerPreferences.TicksPerSecond), nameof(GameSettings.TicksPerSecond), 30, 120, 5, true),
        new SettingFieldSpec(AdvancedSettingsTab.World, "AI Eval Interval", "world", nameof(PlayerPreferences.AIEvalInterval), nameof(GameSettings.AIEvalInterval), 10, 120, 5, true),
        new SettingFieldSpec(AdvancedSettingsTab.World, "Pawn Wander Radius", "world", nameof(PlayerPreferences.PawnWanderRadiusBlocks), nameof(GameSettings.PawnWanderRadiusBlocks), 8, 32, 1, false),

        new SettingFieldSpec(AdvancedSettingsTab.Selection, "Outline Width", "selection", nameof(PlayerPreferences.SelectionOutlineWidth), nameof(GameSettings.SelectionOutlineWidth), 0.001, 0.05, 0.001, false, 3),
        new SettingFieldSpec(AdvancedSettingsTab.Selection, "Outline Offset", "selection", nameof(PlayerPreferences.SelectionOutlineOffset), nameof(GameSettings.SelectionOutlineOffset), 0, 0.08, 0.001, false),
        new SettingFieldSpec(AdvancedSettingsTab.Selection, "Color Red", "selection", nameof(PlayerPreferences.SelectionOutlineColorR), nameof(GameSettings.SelectionOutlineColorR), 0, 1, 0.01, false),
        new SettingFieldSpec(AdvancedSettingsTab.Selection, "Color Green", "selection", nameof(PlayerPreferences.SelectionOutlineColorG), nameof(GameSettings.SelectionOutlineColorG), 0, 1, 0.01, false),
        new SettingFieldSpec(AdvancedSettingsTab.Selection, "Color Blue", "selection", nameof(PlayerPreferences.SelectionOutlineColorB), nameof(GameSettings.SelectionOutlineColorB), 0, 1, 0.01, false),
        new SettingFieldSpec(AdvancedSettingsTab.Selection, "Opacity", "selection", nameof(PlayerPreferences.SelectionOutlineColorA), nameof(GameSettings.SelectionOutlineColorA), 0, 1, 0.01, false),

        new SettingFieldSpec(AdvancedSettingsTab.Hostiles, "Enemy Move Speed", "hostiles", nameof(PlayerPreferences.EnemyBaseMoveSpeedBlocksPerSecond), nameof(GameSettings.EnemyBaseMoveSpeedBlocksPerSecond), 1, 6, 0.1, false),
        new SettingFieldSpec(AdvancedSettingsTab.Hostiles, "Enemy Detection Range", "hostiles", nameof(PlayerPreferences.EnemyDetectionRangeBlocks), nameof(GameSettings.EnemyDetectionRangeBlocks), 5, 40, 1, false),
        new SettingFieldSpec(AdvancedSettingsTab.Hostiles, "Enemy Flee HP", "hostiles", nameof(PlayerPreferences.EnemyFleeHpPercent), nameof(GameSettings.EnemyFleeHpPercent), 0.05, 0.95, 0.05, false),
        new SettingFieldSpec(AdvancedSettingsTab.Hostiles, "Damage Multiplier", "hostiles", nameof(PlayerPreferences.HostileDamageMultiplier), nameof(GameSettings.HostileDamageMultiplier), 0.25, 2, 0.05, false),
        new SettingFieldSpec(AdvancedSettingsTab.Hostiles, "Cooldown Multiplier", "hostiles", nameof(PlayerPreferences.HostileCooldownMultiplier), nameof(GameSettings.HostileCooldownMultiplier), 0.5, 2, 0.05, false),
        new SettingFieldSpec(AdvancedSettingsTab.Hostiles, "Range Multiplier", "hostiles", nameof(PlayerPreferences.HostileRangeMultiplier), nameof(GameSettings.HostileRangeMultiplier), 0.25, 2, 0.05, false),

        new SettingFieldSpec(AdvancedSettingsTab.Raids, "Immediate Attack Chance", "raids", nameof(PlayerPreferences.RaidImmediateAttackChance), nameof(GameSettings.RaidImmediateAttackChance), 0, 1, 0.05, false),
        new SettingFieldSpec(AdvancedSettingsTab.Raids, "Threat Divisor", "raids", nameof(PlayerPreferences.RaidCountThreatDivisor), nameof(GameSettings.RaidCountThreatDivisor), 100, 500, 10, false),
        new SettingFieldSpec(AdvancedSettingsTab.Raids, "Raid Max Count", "raids", nameof(PlayerPreferences.RaidMaxCount), nameof(GameSettings.RaidMaxCount), 1, 12, 1, true),
        new SettingFieldSpec(AdvancedSettingsTab.Raids, "Stat Bonus Cap", "raids", nameof(PlayerPreferences.RaidStatBonusCap), nameof(GameSettings.RaidStatBonusCap), 0, 8, 0.25, false),

        new SettingFieldSpec(AdvancedSettingsTab.Weapons, "Bow Damage", "weapons", nameof(PlayerPreferences.BowBaseDamage), nameof(GameSettings.BowBaseDamage), 1, 30, 1, false),
        new SettingFieldSpec(AdvancedSettingsTab.Weapons, "Bow Cooldown", "weapons", nameof(PlayerPreferences.BowCooldownTicks), nameof(GameSettings.BowCooldownTicks), 30, 240, 5, true),
        new SettingFieldSpec(AdvancedSettingsTab.Weapons, "Crossbow Damage", "weapons", nameof(PlayerPreferences.CrossbowBaseDamage), nameof(GameSettings.CrossbowBaseDamage), 1, 40, 1, false),
        new SettingFieldSpec(AdvancedSettingsTab.Weapons, "Crossbow Cooldown", "weapons", nameof(PlayerPreferences.CrossbowCooldownTicks), nameof(GameSettings.CrossbowCooldownTicks), 30, 300, 5, true),
        new SettingFieldSpec(AdvancedSettingsTab.Weapons, "Rifle Damage", "weapons", nameof(PlayerPreferences.RifleBaseDamage), nameof(GameSettings.RifleBaseDamage), 1, 40, 1, false),
        new SettingFieldSpec(AdvancedSettingsTab.Weapons, "Rifle Cooldown", "weapons", nameof(PlayerPreferences.RifleCooldownTicks), nameof(GameSettings.RifleCooldownTicks), 30, 240, 5, true),

        new SettingFieldSpec(AdvancedSettingsTab.Environment, "Days Per Season", "environment", nameof(PlayerPreferences.DaysPerSeason), nameof(GameSettings.DaysPerSeason), 3, 60, 1, true),
        new SettingFieldSpec(AdvancedSettingsTab.Environment, "Weather Change Hours", "environment", nameof(PlayerPreferences.WeatherChangeIntervalHours), nameof(GameSettings.WeatherChangeIntervalHours), 1, 24, 1, true),
    };
}
