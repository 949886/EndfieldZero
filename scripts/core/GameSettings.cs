using System.Reflection;
using Godot;

namespace Cherry.Core;

/// <summary>
/// Editable game settings stored as a Godot Resource.
/// Also provides the runtime singleton used by the game.
/// </summary>
[GlobalClass]
public partial class GameSettings : Resource
{
    private const string SettingsResourcePath = "res://settings/game_settings.tres";
    private static readonly PropertyInfo[] EditableProperties = typeof(GameSettings)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public);
    private static GameSettings _instance;

    public const int DefaultChunkSizeValue = 64;
    public const int DefaultMaxLayersValue = 4;
    public const float DefaultBlockPixelSizeValue = 1f;
    public const int DefaultLoadRadiusValue = 4;
    public const int DefaultUnloadRadiusValue = 6;
    public const int DefaultMaxChunkLoadsPerFrameValue = 2;
    public const int DefaultTicksPerSecondValue = 60;
    public const int DefaultSeedValue = 42;
    public const int DefaultAIEvalIntervalValue = 30;
    public const float DefaultPawnWanderRadiusBlocksValue = 16f;
    public const float DefaultPawnBaseMoveSpeedBlocksPerSecondValue = 3.125f;
    public const float DefaultEnemyBaseMoveSpeedBlocksPerSecondValue = 2.8f;
    public const int DefaultDaysPerSeasonValue = 15;
    public const int DefaultWeatherChangeIntervalHoursValue = 6;

    public static GameSettings Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = ResourceLoader.Load<GameSettings>(SettingsResourcePath);
            if (_instance == null)
            {
                GD.PushWarning($"[GameSettings] Could not load {SettingsResourcePath}, using built-in defaults.");
                _instance = new GameSettings();
            }

            return _instance;
        }
    }

    // --- Chunk / World ---
    [Export] public int ChunkSize { get; set; } = DefaultChunkSizeValue;
    [Export] public int MaxLayers { get; set; } = DefaultMaxLayersValue;
    [Export] public float BlockPixelSize { get; set; } = DefaultBlockPixelSizeValue;

    // --- Loading ---
    [Export] public int LoadRadius { get; set; } = DefaultLoadRadiusValue;
    [Export] public int UnloadRadius { get; set; } = DefaultUnloadRadiusValue;
    [Export] public int MaxChunkLoadsPerFrame { get; set; } = DefaultMaxChunkLoadsPerFrameValue;

    // --- Time ---
    [Export] public int TicksPerSecond { get; set; } = DefaultTicksPerSecondValue;

    // --- Pawn ---
    [Export] public int AIEvalInterval { get; set; } = DefaultAIEvalIntervalValue;
    [Export] public float PawnWanderRadiusBlocks { get; set; } = DefaultPawnWanderRadiusBlocksValue;

    // --- Hostile Balance ---
    [ExportGroup("Hostile Balance")]
    [Export] public float EnemyBaseMoveSpeedBlocksPerSecond { get; set; } = DefaultEnemyBaseMoveSpeedBlocksPerSecondValue;
    [Export] public float EnemyDetectionRangeBlocks { get; set; } = 20f;
    [Export(PropertyHint.Range, "0.05,0.95,0.05")] public float EnemyFleeHpPercent { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0.1,2.0,0.05")] public float HostileDamageMultiplier { get; set; } = 0.8f;
    [Export(PropertyHint.Range, "0.5,3.0,0.05")] public float HostileCooldownMultiplier { get; set; } = 1.15f;
    [Export(PropertyHint.Range, "0.25,2.0,0.05")] public float HostileRangeMultiplier { get; set; } = 0.9f;
    [Export(PropertyHint.Range, "0.1,1.0,0.05")] public float HostilePreferredRangedDistanceRatio { get; set; } = 0.6f;
    [Export] public int EnemyTargetSearchIntervalTicks { get; set; } = 40;
    [Export(PropertyHint.Range, "0.0,1.0,0.05")] public float RaidImmediateAttackChance { get; set; } = 0.5f;
    [Export] public int HostilePrepareDurationTicks { get; set; } = 300;
    [Export] public float HostilePrepareWanderRadiusBlocks { get; set; } = 6f;
    [Export] public int HostilePrepareRepickTicks { get; set; } = 45;
    [Export] public float HostileBaseHp { get; set; } = 72f;
    [Export] public float HostileHpPerStrength { get; set; } = 3f;
    [Export] public int HostileShootingMin { get; set; } = 1;
    [Export] public int HostileShootingMax { get; set; } = 7;
    [Export] public int HostileStrengthMin { get; set; } = 2;
    [Export] public int HostileStrengthMax { get; set; } = 6;
    [Export] public int HostileAgilityMin { get; set; } = 2;
    [Export] public int HostileAgilityMax { get; set; } = 6;

    // --- Raid Balance ---
    [ExportGroup("Raid Balance")]
    [Export] public float RaidCountThreatDivisor { get; set; } = 220f;
    [Export] public int RaidMaxCount { get; set; } = 6;
    [Export] public float RaidLowThreatWeaponThreshold { get; set; } = 400f;
    [Export] public float RaidMidThreatWeaponThreshold { get; set; } = 800f;
    [Export] public float RaidStatBonusThreatDivisor { get; set; } = 260f;
    [Export] public float RaidStatBonusCap { get; set; } = 3.5f;

    // --- Animal Attack Balance ---
    [ExportGroup("Animal Attack Balance")]
    [Export] public float AnimalAttackCountThreatDivisor { get; set; } = 260f;
    [Export] public int AnimalAttackMaxCount { get; set; } = 3;
    [Export] public int AnimalAttackAgilityMin { get; set; } = 6;
    [Export] public int AnimalAttackAgilityMax { get; set; } = 10;
    [Export] public int AnimalAttackStrengthMin { get; set; } = 5;
    [Export] public int AnimalAttackStrengthMax { get; set; } = 8;

    // --- Weapon Balance ---
    [ExportGroup("Weapon Balance")]
    [Export] public float FistBaseDamage { get; set; } = 5f;
    [Export] public float FistRange { get; set; } = 1f;
    [Export] public int FistCooldownTicks { get; set; } = 60;
    [Export] public float FistAccuracyMod { get; set; } = 1f;
    [Export] public float KnifeBaseDamage { get; set; } = 10f;
    [Export] public float KnifeRange { get; set; } = 1f;
    [Export] public int KnifeCooldownTicks { get; set; } = 55;
    [Export] public float KnifeAccuracyMod { get; set; } = 1f;
    [Export] public float SpearBaseDamage { get; set; } = 12f;
    [Export] public float SpearRange { get; set; } = 1.5f;
    [Export] public int SpearCooldownTicks { get; set; } = 70;
    [Export] public float SpearAccuracyMod { get; set; } = 1f;
    [Export] public float HammerBaseDamage { get; set; } = 15f;
    [Export] public float HammerRange { get; set; } = 1f;
    [Export] public int HammerCooldownTicks { get; set; } = 110;
    [Export] public float HammerAccuracyMod { get; set; } = 0.85f;
    [Export] public float BowBaseDamage { get; set; } = 9f;
    [Export] public float BowRange { get; set; } = 13f;
    [Export] public int BowCooldownTicks { get; set; } = 135;
    [Export] public float BowAccuracyMod { get; set; } = 0.8f;
    [Export] public float CrossbowBaseDamage { get; set; } = 12f;
    [Export] public float CrossbowRange { get; set; } = 17f;
    [Export] public int CrossbowCooldownTicks { get; set; } = 200;
    [Export] public float CrossbowAccuracyMod { get; set; } = 0.82f;
    [Export] public float RifleBaseDamage { get; set; } = 14f;
    [Export] public float RifleRange { get; set; } = 18f;
    [Export] public int RifleCooldownTicks { get; set; } = 105;
    [Export] public float RifleAccuracyMod { get; set; } = 0.9f;

    // --- Environment ---
    [Export] public int DaysPerSeason { get; set; } = DefaultDaysPerSeasonValue;
    [Export] public int WeatherChangeIntervalHours { get; set; } = DefaultWeatherChangeIntervalHoursValue;

    public static void Reload()
    {
        _instance = null;
    }

    public static GameSettings CreateDefaultSnapshot()
    {
        GameSettings defaults = ResourceLoader.Load<GameSettings>(SettingsResourcePath);
        return defaults?.Duplicate(true) as GameSettings ?? new GameSettings();
    }

    public void CopyFrom(GameSettings source)
    {
        if (source == null)
            return;

        foreach (PropertyInfo property in EditableProperties)
        {
            if (!property.CanRead || !property.CanWrite)
                continue;
            if (property.GetMethod?.IsStatic == true || property.SetMethod?.IsStatic == true)
                continue;

            property.SetValue(this, property.GetValue(source));
        }

        EmitChanged();
    }

    public static void ApplyUserOverrides(PlayerPreferences preferences)
    {
        if (preferences == null)
            return;

        var target = Instance;
        target.CopyFrom(CreateDefaultSnapshot());
        foreach (var spec in SettingsFieldRegistry.AdvancedFields)
            spec.ApplyTo(target, preferences);
        target.EmitChanged();
    }
}

/// <summary>
/// Compatibility shim so the rest of the project can keep using the old static access pattern.
/// </summary>
public static class Settings
{
    public static int ChunkSize => GameSettings.Instance.ChunkSize;
    public static int MaxLayers => GameSettings.Instance.MaxLayers;
    public static float BlockPixelSize => GameSettings.Instance.BlockPixelSize;
    public static float ChunkPixelSize => ChunkSize * BlockPixelSize;
    public static int LoadRadius => GameSettings.Instance.LoadRadius;
    public static int UnloadRadius => GameSettings.Instance.UnloadRadius;
    public static int MaxChunkLoadsPerFrame => GameSettings.Instance.MaxChunkLoadsPerFrame;
    public static int TicksPerSecond => GameSettings.Instance.TicksPerSecond;
    public static float TickInterval => 1f / TicksPerSecond;
    public static int AIEvalInterval => GameSettings.Instance.AIEvalInterval;
    public static float PawnWanderRadius => GameSettings.Instance.PawnWanderRadiusBlocks * BlockPixelSize;
    public static float EnemyBaseMoveSpeed => GameSettings.Instance.EnemyBaseMoveSpeedBlocksPerSecond;
    public static float EnemyDetectionRange => GameSettings.Instance.EnemyDetectionRangeBlocks * BlockPixelSize;
    public static float EnemyFleeHpPercent => GameSettings.Instance.EnemyFleeHpPercent;
    public static float HostileDamageMultiplier => GameSettings.Instance.HostileDamageMultiplier;
    public static float HostileCooldownMultiplier => GameSettings.Instance.HostileCooldownMultiplier;
    public static float HostileRangeMultiplier => GameSettings.Instance.HostileRangeMultiplier;
    public static float HostilePreferredRangedDistanceRatio => GameSettings.Instance.HostilePreferredRangedDistanceRatio;
    public static int EnemyTargetSearchIntervalTicks => GameSettings.Instance.EnemyTargetSearchIntervalTicks;
    public static float RaidImmediateAttackChance => GameSettings.Instance.RaidImmediateAttackChance;
    public static int HostilePrepareDurationTicks => GameSettings.Instance.HostilePrepareDurationTicks;
    public static float HostilePrepareWanderRadius => GameSettings.Instance.HostilePrepareWanderRadiusBlocks * BlockPixelSize;
    public static int HostilePrepareRepickTicks => GameSettings.Instance.HostilePrepareRepickTicks;
    public static float HostileBaseHp => GameSettings.Instance.HostileBaseHp;
    public static float HostileHpPerStrength => GameSettings.Instance.HostileHpPerStrength;
    public static int HostileShootingMin => GameSettings.Instance.HostileShootingMin;
    public static int HostileShootingMax => GameSettings.Instance.HostileShootingMax;
    public static int HostileStrengthMin => GameSettings.Instance.HostileStrengthMin;
    public static int HostileStrengthMax => GameSettings.Instance.HostileStrengthMax;
    public static int HostileAgilityMin => GameSettings.Instance.HostileAgilityMin;
    public static int HostileAgilityMax => GameSettings.Instance.HostileAgilityMax;
    public static float RaidCountThreatDivisor => GameSettings.Instance.RaidCountThreatDivisor;
    public static int RaidMaxCount => GameSettings.Instance.RaidMaxCount;
    public static float RaidLowThreatWeaponThreshold => GameSettings.Instance.RaidLowThreatWeaponThreshold;
    public static float RaidMidThreatWeaponThreshold => GameSettings.Instance.RaidMidThreatWeaponThreshold;
    public static float RaidStatBonusThreatDivisor => GameSettings.Instance.RaidStatBonusThreatDivisor;
    public static float RaidStatBonusCap => GameSettings.Instance.RaidStatBonusCap;
    public static float AnimalAttackCountThreatDivisor => GameSettings.Instance.AnimalAttackCountThreatDivisor;
    public static int AnimalAttackMaxCount => GameSettings.Instance.AnimalAttackMaxCount;
    public static int AnimalAttackAgilityMin => GameSettings.Instance.AnimalAttackAgilityMin;
    public static int AnimalAttackAgilityMax => GameSettings.Instance.AnimalAttackAgilityMax;
    public static int AnimalAttackStrengthMin => GameSettings.Instance.AnimalAttackStrengthMin;
    public static int AnimalAttackStrengthMax => GameSettings.Instance.AnimalAttackStrengthMax;
    public static float FistBaseDamage => GameSettings.Instance.FistBaseDamage;
    public static float FistRange => GameSettings.Instance.FistRange;
    public static int FistCooldownTicks => GameSettings.Instance.FistCooldownTicks;
    public static float FistAccuracyMod => GameSettings.Instance.FistAccuracyMod;
    public static float KnifeBaseDamage => GameSettings.Instance.KnifeBaseDamage;
    public static float KnifeRange => GameSettings.Instance.KnifeRange;
    public static int KnifeCooldownTicks => GameSettings.Instance.KnifeCooldownTicks;
    public static float KnifeAccuracyMod => GameSettings.Instance.KnifeAccuracyMod;
    public static float SpearBaseDamage => GameSettings.Instance.SpearBaseDamage;
    public static float SpearRange => GameSettings.Instance.SpearRange;
    public static int SpearCooldownTicks => GameSettings.Instance.SpearCooldownTicks;
    public static float SpearAccuracyMod => GameSettings.Instance.SpearAccuracyMod;
    public static float HammerBaseDamage => GameSettings.Instance.HammerBaseDamage;
    public static float HammerRange => GameSettings.Instance.HammerRange;
    public static int HammerCooldownTicks => GameSettings.Instance.HammerCooldownTicks;
    public static float HammerAccuracyMod => GameSettings.Instance.HammerAccuracyMod;
    public static float BowBaseDamage => GameSettings.Instance.BowBaseDamage;
    public static float BowRange => GameSettings.Instance.BowRange;
    public static int BowCooldownTicks => GameSettings.Instance.BowCooldownTicks;
    public static float BowAccuracyMod => GameSettings.Instance.BowAccuracyMod;
    public static float CrossbowBaseDamage => GameSettings.Instance.CrossbowBaseDamage;
    public static float CrossbowRange => GameSettings.Instance.CrossbowRange;
    public static int CrossbowCooldownTicks => GameSettings.Instance.CrossbowCooldownTicks;
    public static float CrossbowAccuracyMod => GameSettings.Instance.CrossbowAccuracyMod;
    public static float RifleBaseDamage => GameSettings.Instance.RifleBaseDamage;
    public static float RifleRange => GameSettings.Instance.RifleRange;
    public static int RifleCooldownTicks => GameSettings.Instance.RifleCooldownTicks;
    public static float RifleAccuracyMod => GameSettings.Instance.RifleAccuracyMod;
    public static int DaysPerSeason => GameSettings.Instance.DaysPerSeason;
    public static int WeatherChangeIntervalHours => GameSettings.Instance.WeatherChangeIntervalHours;
}
