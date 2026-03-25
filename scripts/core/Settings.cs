using Godot;

namespace EndfieldZero.Core;

/// <summary>
/// Runtime settings accessor.
/// </summary>
public static class Settings
{
    private const string SettingsResourcePath = "res://settings/game_settings.tres";
    private static GameSettingsResource _resource;

    private static GameSettingsResource Resource
    {
        get
        {
            if (_resource != null) return _resource;

            _resource = ResourceLoader.Load<GameSettingsResource>(SettingsResourcePath);
            if (_resource == null)
            {
                GD.PushWarning($"[Settings] Could not load {SettingsResourcePath}, using built-in defaults.");
                _resource = new GameSettingsResource();
            }

            return _resource;
        }
    }

    // --- Chunk / World ---
    public static int ChunkSize => Resource.ChunkSize;
    public static int MaxLayers => Resource.MaxLayers;
    public static float BlockPixelSize => Resource.BlockPixelSize;
    public static float ChunkPixelSize => ChunkSize * BlockPixelSize;

    // --- Loading ---
    public static int LoadRadius => Resource.LoadRadius;
    public static int UnloadRadius => Resource.UnloadRadius;
    public static int MaxChunkLoadsPerFrame => Resource.MaxChunkLoadsPerFrame;

    // --- Time ---
    public static int TicksPerSecond => Resource.TicksPerSecond;
    public static float TickInterval => 1f / TicksPerSecond;

    // --- World Generation ---
    public static int DefaultSeed => Resource.DefaultSeed;

    // --- Pawn ---
    public static int AIEvalInterval => Resource.AIEvalInterval;
    public static float PawnWanderRadius => Resource.PawnWanderRadiusBlocks * BlockPixelSize;
    public static float PawnBaseMoveSpeed => Resource.PawnBaseMoveSpeedBlocksPerSecond * BlockPixelSize;

    // --- Environment ---
    public static int DaysPerSeason => Resource.DaysPerSeason;
    public static int WeatherChangeIntervalHours => Resource.WeatherChangeIntervalHours;
}
