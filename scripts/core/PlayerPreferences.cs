using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Core;

public enum DisplayModePreference
{
    Windowed,
    Fullscreen,
    BorderlessFullscreen,
}

public sealed class PlayerPreferences
{
    public DisplayModePreference DisplayMode { get; set; } = DisplayModePreference.Windowed;
    public Vector2I WindowSize { get; set; } = new(1600, 900);
    public bool VSyncEnabled { get; set; } = true;
    public int FpsLimit { get; set; } = 60;
    public CameraViewMode DefaultViewMode { get; set; } = CameraViewMode.Orthographic3D;

    public float MasterVolume { get; set; } = 1f;
    public float MusicVolume { get; set; } = 1f;
    public float SfxVolume { get; set; } = 1f;
    public bool AudioInBackground { get; set; } = true;

    public bool ShowTutorial { get; set; } = true;
    public bool ShowDebugHud { get; set; } = true;
    public bool ShowAdvancedSettings { get; set; }

    public int TicksPerSecond { get; set; } = GameSettings.DefaultTicksPerSecondValue;
    public int AIEvalInterval { get; set; } = GameSettings.DefaultAIEvalIntervalValue;
    public float PawnWanderRadiusBlocks { get; set; } = GameSettings.DefaultPawnWanderRadiusBlocksValue;

    public float EnemyBaseMoveSpeedBlocksPerSecond { get; set; } = GameSettings.DefaultEnemyBaseMoveSpeedBlocksPerSecondValue;
    public float EnemyDetectionRangeBlocks { get; set; } = 20f;
    public float EnemyFleeHpPercent { get; set; } = 0.35f;
    public float HostileDamageMultiplier { get; set; } = 0.8f;
    public float HostileCooldownMultiplier { get; set; } = 1.15f;
    public float HostileRangeMultiplier { get; set; } = 0.9f;

    public float RaidImmediateAttackChance { get; set; } = 0.5f;
    public float RaidCountThreatDivisor { get; set; } = 220f;
    public int RaidMaxCount { get; set; } = 6;
    public float RaidStatBonusCap { get; set; } = 3.5f;

    public float BowBaseDamage { get; set; } = 9f;
    public int BowCooldownTicks { get; set; } = 135;
    public float CrossbowBaseDamage { get; set; } = 12f;
    public int CrossbowCooldownTicks { get; set; } = 200;
    public float RifleBaseDamage { get; set; } = 14f;
    public int RifleCooldownTicks { get; set; } = 105;

    public int DaysPerSeason { get; set; } = GameSettings.DefaultDaysPerSeasonValue;
    public int WeatherChangeIntervalHours { get; set; } = GameSettings.DefaultWeatherChangeIntervalHoursValue;

    public PlayerPreferences Clone()
    {
        return new PlayerPreferences
        {
            DisplayMode = DisplayMode,
            WindowSize = WindowSize,
            VSyncEnabled = VSyncEnabled,
            FpsLimit = FpsLimit,
            DefaultViewMode = DefaultViewMode,
            MasterVolume = MasterVolume,
            MusicVolume = MusicVolume,
            SfxVolume = SfxVolume,
            AudioInBackground = AudioInBackground,
            ShowTutorial = ShowTutorial,
            ShowDebugHud = ShowDebugHud,
            ShowAdvancedSettings = ShowAdvancedSettings,
            TicksPerSecond = TicksPerSecond,
            AIEvalInterval = AIEvalInterval,
            PawnWanderRadiusBlocks = PawnWanderRadiusBlocks,
            EnemyBaseMoveSpeedBlocksPerSecond = EnemyBaseMoveSpeedBlocksPerSecond,
            EnemyDetectionRangeBlocks = EnemyDetectionRangeBlocks,
            EnemyFleeHpPercent = EnemyFleeHpPercent,
            HostileDamageMultiplier = HostileDamageMultiplier,
            HostileCooldownMultiplier = HostileCooldownMultiplier,
            HostileRangeMultiplier = HostileRangeMultiplier,
            RaidImmediateAttackChance = RaidImmediateAttackChance,
            RaidCountThreatDivisor = RaidCountThreatDivisor,
            RaidMaxCount = RaidMaxCount,
            RaidStatBonusCap = RaidStatBonusCap,
            BowBaseDamage = BowBaseDamage,
            BowCooldownTicks = BowCooldownTicks,
            CrossbowBaseDamage = CrossbowBaseDamage,
            CrossbowCooldownTicks = CrossbowCooldownTicks,
            RifleBaseDamage = RifleBaseDamage,
            RifleCooldownTicks = RifleCooldownTicks,
            DaysPerSeason = DaysPerSeason,
            WeatherChangeIntervalHours = WeatherChangeIntervalHours,
        };
    }

    public bool ContentEquals(PlayerPreferences other)
    {
        if (other == null)
            return false;

        return DisplayMode == other.DisplayMode
            && WindowSize == other.WindowSize
            && VSyncEnabled == other.VSyncEnabled
            && FpsLimit == other.FpsLimit
            && DefaultViewMode == other.DefaultViewMode
            && Mathf.IsEqualApprox(MasterVolume, other.MasterVolume)
            && Mathf.IsEqualApprox(MusicVolume, other.MusicVolume)
            && Mathf.IsEqualApprox(SfxVolume, other.SfxVolume)
            && AudioInBackground == other.AudioInBackground
            && ShowTutorial == other.ShowTutorial
            && ShowDebugHud == other.ShowDebugHud
            && ShowAdvancedSettings == other.ShowAdvancedSettings
            && TicksPerSecond == other.TicksPerSecond
            && AIEvalInterval == other.AIEvalInterval
            && Mathf.IsEqualApprox(PawnWanderRadiusBlocks, other.PawnWanderRadiusBlocks)
            && Mathf.IsEqualApprox(EnemyBaseMoveSpeedBlocksPerSecond, other.EnemyBaseMoveSpeedBlocksPerSecond)
            && Mathf.IsEqualApprox(EnemyDetectionRangeBlocks, other.EnemyDetectionRangeBlocks)
            && Mathf.IsEqualApprox(EnemyFleeHpPercent, other.EnemyFleeHpPercent)
            && Mathf.IsEqualApprox(HostileDamageMultiplier, other.HostileDamageMultiplier)
            && Mathf.IsEqualApprox(HostileCooldownMultiplier, other.HostileCooldownMultiplier)
            && Mathf.IsEqualApprox(HostileRangeMultiplier, other.HostileRangeMultiplier)
            && Mathf.IsEqualApprox(RaidImmediateAttackChance, other.RaidImmediateAttackChance)
            && Mathf.IsEqualApprox(RaidCountThreatDivisor, other.RaidCountThreatDivisor)
            && RaidMaxCount == other.RaidMaxCount
            && Mathf.IsEqualApprox(RaidStatBonusCap, other.RaidStatBonusCap)
            && Mathf.IsEqualApprox(BowBaseDamage, other.BowBaseDamage)
            && BowCooldownTicks == other.BowCooldownTicks
            && Mathf.IsEqualApprox(CrossbowBaseDamage, other.CrossbowBaseDamage)
            && CrossbowCooldownTicks == other.CrossbowCooldownTicks
            && Mathf.IsEqualApprox(RifleBaseDamage, other.RifleBaseDamage)
            && RifleCooldownTicks == other.RifleCooldownTicks
            && DaysPerSeason == other.DaysPerSeason
            && WeatherChangeIntervalHours == other.WeatherChangeIntervalHours;
    }
}
