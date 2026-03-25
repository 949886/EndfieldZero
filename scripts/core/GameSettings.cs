using Godot;

namespace EndfieldZero.Core;

/// <summary>
/// Editable game settings stored as a Godot Resource.
/// Also provides the runtime singleton used by the game.
/// </summary>
[GlobalClass]
public partial class GameSettings : Resource
{
    private const string SettingsResourcePath = "res://settings/game_settings.tres";
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

    // --- Environment ---
    [Export] public int DaysPerSeason { get; set; } = DefaultDaysPerSeasonValue;
    [Export] public int WeatherChangeIntervalHours { get; set; } = DefaultWeatherChangeIntervalHoursValue;

    public static void Reload()
    {
        _instance = null;
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
    public static int DaysPerSeason => GameSettings.Instance.DaysPerSeason;
    public static int WeatherChangeIntervalHours => GameSettings.Instance.WeatherChangeIntervalHours;
}
