namespace EndfieldZero.Core;

/// <summary>
/// Global constants for the game.
/// </summary>
public static class Constants
{
    // --- Chunk / World ---
    public const int ChunkSize = 64;              // 64×64 blocks per chunk
    public const int MaxLayers = 4;               // 预留多层（当前只用 layer 0）
    public const float BlockPixelSize = 32f;      // 每个 block 的像素尺寸
    public const float ChunkPixelSize = ChunkSize * BlockPixelSize; // 2048px per chunk

    // --- Loading ---
    public const int LoadRadius = 4;              // 加载半径（chunk 数）
    public const int UnloadRadius = 6;            // 卸载半径
    public const int MaxChunkLoadsPerFrame = 2;   // 每帧最多加载 N 个 chunk

    // --- Time ---
    public const int TicksPerSecond = 60;
    public const float TickInterval = 1f / TicksPerSecond;

    // --- World Generation ---
    public const int DefaultSeed = 42;
}
