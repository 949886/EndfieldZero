using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Core;
using EndfieldZero.Pathfinding;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Manages the chunk lifecycle: loading, unloading, and rendering chunks
/// around the camera. Chunks are loaded in a spiral pattern from the center
/// outward, with a maximum of N loads per frame to avoid stuttering.
/// Uses Node3D — chunks are placed on the XZ plane.
/// </summary>
public partial class WorldManager : Node3D
{
    /// <summary>Singleton instance.</summary>
    public static WorldManager Instance { get; private set; }
    /// <summary>Currently loaded chunks keyed by chunk coordinate.</summary>
    private readonly Dictionary<Vector2I, Chunk> _loadedChunks = new();

    /// <summary>Node holding chunk renderer children.</summary>
    private readonly Dictionary<Vector2I, ChunkRenderer> _renderers = new();

    /// <summary>Procedural generator.</summary>
    private WorldGenerator _generator;

    /// <summary>Chunk cache that restores unloaded data and persists modified chunks.</summary>
    private ChunkCache _chunkCache;

    /// <summary>Queue of chunk coordinates waiting to be loaded.</summary>
    private readonly Queue<Vector2I> _loadQueue = new();

    /// <summary>Reference to the camera for determining load center.</summary>
    private Camera3D _camera;

    /// <summary>Last known camera chunk position, to avoid redundant recalculation.</summary>
    private Vector2I _lastCameraChunkCoord = new(int.MinValue, int.MinValue);

    [Export] public int Seed { get; set; }　= 42;
    [Export(PropertyHint.Range, "16,2048,16")] public int MaxCachedChunksInMemory { get; set; } = 256;
    [Export] public bool PersistModifiedChunksToDisk { get; set; } = true;

    /// <summary>Controls biome size. Higher = larger biomes. Default: 3.0.</summary>
    [Export(PropertyHint.Range, "0.5,20.0,0.5")] public float BiomeScale { get; set; } = 1.0f;

    /// <summary>Fractal octaves for biome noise. Fewer = smoother. Default: 2.</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int BiomeOctaves { get; set; } = 4;

    /// <summary>Continent-level scale. Higher = larger landmasses. Default: 5.0.</summary>
    [Export(PropertyHint.Range, "1.0,30.0,0.5")] public float ContinentScale { get; set; } = 5.0f;

    public override void _Ready()
    {
        Instance = this;
        _generator = new WorldGenerator(Seed, BiomeScale, BiomeOctaves, ContinentScale);
        _chunkCache = new ChunkCache(Seed, MaxCachedChunksInMemory, PersistModifiedChunksToDisk);
        _camera = GetViewport().GetCamera3D();

        // Initialize pathfinding service
        PathfindingService.Instance = new PathfindingService(this);
    }

    public override void _ExitTree()
    {
        foreach (var chunk in _loadedChunks.Values)
            _chunkCache?.Store(chunk);

        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera3D();
            if (_camera == null) return;
        }

        // Camera looks down -Y, so its XZ position maps to chunk coordinates
        Vector2I currentChunkCoord = WorldToChunkCoord(_camera.GlobalPosition);

        // Recalculate load queue when camera moves to a new chunk
        if (currentChunkCoord != _lastCameraChunkCoord)
        {
            _lastCameraChunkCoord = currentChunkCoord;
            RebuildLoadQueue(currentChunkCoord);
            UnloadDistantChunks(currentChunkCoord);
        }

        // Load a few chunks per frame from the queue
        int loaded = 0;
        while (_loadQueue.Count > 0 && loaded < Settings.MaxChunkLoadsPerFrame)
        {
            Vector2I coord = _loadQueue.Dequeue();

            // Skip if already loaded or out of range
            if (_loadedChunks.ContainsKey(coord))
                continue;

            if (ChunkDistance(coord, _lastCameraChunkCoord) > Settings.LoadRadius)
                continue;

            LoadChunk(coord);
            loaded++;
        }

        // Rebuild dirty chunk meshes (limit per frame to avoid spikes)
        int rebuilt = 0;
        foreach (var kvp in _renderers)
        {
            if (kvp.Value != null && _loadedChunks.TryGetValue(kvp.Key, out var chunk) && chunk.IsDirty)
            {
                kvp.Value.RebuildMesh();
                rebuilt++;
                if (rebuilt >= Settings.MaxChunkLoadsPerFrame) break;
            }
        }
    }

    // --- Loading / Unloading ---

    private void LoadChunk(Vector2I chunkCoord)
    {
        var chunk = new Chunk(chunkCoord);
        bool restoredFromCache = _chunkCache != null && _chunkCache.TryRestore(chunkCoord, chunk);
        if (!restoredFromCache)
            _generator.GenerateChunk(chunk);

        _loadedChunks[chunkCoord] = chunk;

        // Create renderer node
        var renderer = new ChunkRenderer();
        renderer.SetChunk(chunk);
        renderer.Position = chunk.WorldPosition3D;
        AddChild(renderer);
        _renderers[chunkCoord] = renderer;

        // Mesh will be built on next _Process dirty check
    }

    private void UnloadDistantChunks(Vector2I centerChunkCoord)
    {
        var toRemove = new List<Vector2I>();

        foreach (var coord in _loadedChunks.Keys)
        {
            if (ChunkDistance(coord, centerChunkCoord) > Settings.UnloadRadius)
            {
                toRemove.Add(coord);
            }
        }

        foreach (var coord in toRemove)
        {
            if (_loadedChunks.TryGetValue(coord, out var chunk))
                _chunkCache?.Store(chunk);

            _loadedChunks.Remove(coord);

            if (_renderers.TryGetValue(coord, out var renderer))
            {
                renderer.QueueFree();
                _renderers.Remove(coord);
            }
        }
    }

    /// <summary>
    /// Build a spiral-ordered load queue emanating from center.
    /// Ensures closest chunks load first.
    /// </summary>
    private void RebuildLoadQueue(Vector2I center)
    {
        _loadQueue.Clear();

        // Generate coordinates in spiral order
        var coords = new List<Vector2I>();
        int r = Settings.LoadRadius;

        for (int dz = -r; dz <= r; dz++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                var coord = new Vector2I(center.X + dx, center.Y + dz);
                coords.Add(coord);
            }
        }

        // Sort by distance to center (spiral-like ordering)
        coords.Sort((a, b) =>
        {
            float distA = (a - center).LengthSquared();
            float distB = (b - center).LengthSquared();
            return distA.CompareTo(distB);
        });

        foreach (var coord in coords)
        {
            if (!_loadedChunks.ContainsKey(coord))
                _loadQueue.Enqueue(coord);
        }
    }

    // --- Coordinate Utilities ---

    /// <summary>Convert 3D world position (XZ plane) to chunk coordinate.</summary>
    public static Vector2I WorldToChunkCoord(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.X / Settings.ChunkPixelSize);
        int cz = Mathf.FloorToInt(worldPos.Z / Settings.ChunkPixelSize);
        return new Vector2I(cx, cz);
    }

    /// <summary>Convert world block coordinate to chunk coordinate.</summary>
    public static Vector2I BlockToChunkCoord(int worldX, int worldZ)
    {
        int cx = worldX >= 0 ? worldX / Settings.ChunkSize : (worldX - Settings.ChunkSize + 1) / Settings.ChunkSize;
        int cz = worldZ >= 0 ? worldZ / Settings.ChunkSize : (worldZ - Settings.ChunkSize + 1) / Settings.ChunkSize;
        return new Vector2I(cx, cz);
    }

    /// <summary>Convert world block coordinate to local coordinate within its chunk.</summary>
    public static Vector2I BlockToLocalCoord(int worldX, int worldZ)
    {
        int lx = ((worldX % Settings.ChunkSize) + Settings.ChunkSize) % Settings.ChunkSize;
        int lz = ((worldZ % Settings.ChunkSize) + Settings.ChunkSize) % Settings.ChunkSize;
        return new Vector2I(lx, lz);
    }

    /// <summary>Get a block at world block coordinates.</summary>
    public Block GetBlock(int worldX, int worldZ, int layer = 0)
    {
        var chunkCoord = BlockToChunkCoord(worldX, worldZ);
        if (!_loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return Block.Air;

        var local = BlockToLocalCoord(worldX, worldZ);
        return chunk.GetBlock(local.X, local.Y, layer);
    }

    /// <summary>Set a block at world block coordinates. Marks the chunk dirty.</summary>
    public void SetBlock(int worldX, int worldZ, Block block, int layer = 0)
    {
        var chunkCoord = BlockToChunkCoord(worldX, worldZ);
        if (!_loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return;

        var local = BlockToLocalCoord(worldX, worldZ);
        chunk.SetBlock(local.X, local.Y, block, layer);
    }

    /// <summary>Chebyshev distance between two chunk coordinates.</summary>
    private static int ChunkDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }
}
