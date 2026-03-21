using Godot;

namespace EndfieldZero.Core;

/// <summary>
/// Editable game settings stored as a Godot Resource.
/// Values are loaded from res://settings/game_settings.tres.
/// </summary>
[GlobalClass]
public partial class GameSettingsResource : Resource
{
    // --- Chunk / World ---
    [Export] public int ChunkSize { get; set; } = 64;
    [Export] public int MaxLayers { get; set; } = 4;
    [Export] public float BlockPixelSize { get; set; } = 1f;

    // --- Loading ---
    [Export] public int LoadRadius { get; set; } = 4;
    [Export] public int UnloadRadius { get; set; } = 6;
    [Export] public int MaxChunkLoadsPerFrame { get; set; } = 2;

    // --- Time ---
    [Export] public int TicksPerSecond { get; set; } = 60;

    // --- World Generation ---
    [Export] public int DefaultSeed { get; set; } = 42;

    // --- Pawn ---
    [Export] public int AIEvalInterval { get; set; } = 30;
    [Export] public float PawnWanderRadiusBlocks { get; set; } = 16f;
    [Export] public float PawnBaseMoveSpeedBlocksPerSecond { get; set; } = 3.125f;
}
