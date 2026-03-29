using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Core;
using EndfieldZero.Pathfinding;
using Godot;

namespace EndfieldZero.World;

public readonly struct SurfaceColumnInfo
{
    public SurfaceColumnInfo(
        Vector2I blockCoord,
        Block block,
        BlockDef def,
        int layer,
        float baseY,
        float surfaceY)
    {
        BlockCoord = blockCoord;
        Block = block;
        Def = def;
        Layer = layer;
        BaseY = baseY;
        SurfaceY = surfaceY;
    }

    public Vector2I BlockCoord { get; }
    public Block Block { get; }
    public BlockDef Def { get; }
    public int Layer { get; }
    public float BaseY { get; }
    public float SurfaceY { get; }
    public bool HasSurface => Def != null && !Block.IsAir;
    public bool IsOccluder => Def != null && Def.IsOccluder(Settings.BlockPixelSize);
}

public readonly struct SurfaceHit
{
    public SurfaceHit(
        bool hit,
        Vector2I blockCoord,
        Vector3 worldPosition,
        Vector3 blockCenterWorld,
        float surfaceY,
        SurfaceColumnInfo column)
    {
        Hit = hit;
        BlockCoord = blockCoord;
        WorldPosition = worldPosition;
        BlockCenterWorld = blockCenterWorld;
        SurfaceY = surfaceY;
        Column = column;
    }

    public bool Hit { get; }
    public Vector2I BlockCoord { get; }
    public Vector3 WorldPosition { get; }
    public Vector3 BlockCenterWorld { get; }
    public float SurfaceY { get; }
    public SurfaceColumnInfo Column { get; }
}

/// <summary>
/// Manages the chunk lifecycle: loading, unloading, and rendering chunks
/// around the camera. Chunks are loaded in a spiral pattern from the center
/// outward, with a maximum of N loads per frame to avoid stuttering.
/// </summary>
public partial class WorldManager : Node3D
{
    private const float HitRayStepFactor = 0.2f;

    public static WorldManager Instance { get; private set; }

    private readonly Dictionary<Vector2I, Chunk> _loadedChunks = new();
    private readonly Dictionary<Vector2I, ChunkRenderer> _renderers = new();
    private readonly Queue<Vector2I> _loadQueue = new();

    private WorldGenerator _generator;
    private ChunkCache _chunkCache;
    private Camera3D _camera;
    private Vector2I _lastCameraChunkCoord = new(int.MinValue, int.MinValue);

    [Export] public int Seed { get; set; } = 42;
    [Export(PropertyHint.Range, "16,2048,16")] public int MaxCachedChunksInMemory { get; set; } = 256;
    [Export] public bool PersistModifiedChunksToDisk { get; set; } = true;
    [Export(PropertyHint.Range, "0.5,20.0,0.5")] public float BiomeScale { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "1,6,1")] public int BiomeOctaves { get; set; } = 4;
    [Export(PropertyHint.Range, "1.0,30.0,0.5")] public float ContinentScale { get; set; } = 5.0f;

    public override void _Ready()
    {
        Instance = this;
        _generator = new WorldGenerator(Seed, BiomeScale, BiomeOctaves, ContinentScale);
        _chunkCache = new ChunkCache(Seed, MaxCachedChunksInMemory, PersistModifiedChunksToDisk);
        _camera = GetViewport().GetCamera3D();

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
            if (_camera == null)
                return;
        }

        Vector3 loadCenter = _camera is GameCamera gameCamera
            ? gameCamera.FocusWorldPosition
            : _camera.GlobalPosition;
        Vector2I currentChunkCoord = WorldToChunkCoord(loadCenter);

        if (currentChunkCoord != _lastCameraChunkCoord)
        {
            _lastCameraChunkCoord = currentChunkCoord;
            RebuildLoadQueue(currentChunkCoord);
            UnloadDistantChunks(currentChunkCoord);
        }

        int loaded = 0;
        while (_loadQueue.Count > 0 && loaded < Settings.MaxChunkLoadsPerFrame)
        {
            Vector2I coord = _loadQueue.Dequeue();
            if (_loadedChunks.ContainsKey(coord))
                continue;
            if (ChunkDistance(coord, _lastCameraChunkCoord) > Settings.LoadRadius)
                continue;

            LoadChunk(coord);
            loaded++;
        }

        int rebuilt = 0;
        foreach (var kvp in _renderers)
        {
            if (kvp.Value != null && _loadedChunks.TryGetValue(kvp.Key, out var chunk) && chunk.IsDirty)
            {
                kvp.Value.RebuildMesh();
                rebuilt++;
                if (rebuilt >= Settings.MaxChunkLoadsPerFrame)
                    break;
            }
        }
    }

    public SurfaceHit ScreenToBlockHit(Vector2 screenPos, Camera3D camera = null)
    {
        camera ??= GetViewport().GetCamera3D();
        if (camera == null)
            return default;

        Vector3 from = camera.ProjectRayOrigin(screenPos);
        Vector3 dir = camera.ProjectRayNormal(screenPos);
        float step = Mathf.Max(Settings.BlockPixelSize * HitRayStepFactor, 0.1f);
        float maxDistance = Mathf.Max(camera.Far, Settings.BlockPixelSize * 128f);

        for (float t = 0f; t <= maxDistance; t += step)
        {
            Vector3 sample = from + dir * t;
            Vector2I coord = PathfindingService.WorldToBlock(sample);
            SurfaceColumnInfo column = GetSurfaceColumnInfo(coord.X, coord.Y);
            float targetY = column.HasSurface ? column.SurfaceY : 0f;

            if (sample.Y > targetY + step * 0.5f)
                continue;

            Vector3 hitWorld = IntersectRayWithHorizontalPlane(from, dir, targetY);
            Vector2I hitCoord = PathfindingService.WorldToBlock(hitWorld);
            SurfaceColumnInfo hitColumn = GetSurfaceColumnInfo(hitCoord.X, hitCoord.Y);
            float hitSurfaceY = hitColumn.HasSurface ? hitColumn.SurfaceY : 0f;
            Vector3 blockCenter = GetBlockCenterWorld(hitCoord.X, hitCoord.Y, hitSurfaceY);

            return new SurfaceHit(true, hitCoord, hitWorld, blockCenter, hitSurfaceY, hitColumn);
        }

        Vector3 fallback = IntersectRayWithHorizontalPlane(from, dir, 0f);
        Vector2I fallbackCoord = PathfindingService.WorldToBlock(fallback);
        SurfaceColumnInfo fallbackColumn = GetSurfaceColumnInfo(fallbackCoord.X, fallbackCoord.Y);
        float fallbackSurfaceY = fallbackColumn.HasSurface ? fallbackColumn.SurfaceY : 0f;
        return new SurfaceHit(
            true,
            fallbackCoord,
            fallback,
            GetBlockCenterWorld(fallbackCoord.X, fallbackCoord.Y, fallbackSurfaceY),
            fallbackSurfaceY,
            fallbackColumn);
    }

    public SurfaceColumnInfo GetSurfaceColumnInfo(int worldX, int worldZ)
    {
        var blockCoord = new Vector2I(worldX, worldZ);

        for (int layer = Settings.MaxLayers - 1; layer >= 0; layer--)
        {
            Block block = GetBlock(worldX, worldZ, layer);
            if (block.IsAir)
                continue;

            BlockDef def = BlockRegistry.Instance.GetDef(block.TypeId);
            if (def == null)
                continue;

            float baseY = layer * Settings.BlockPixelSize;
            float surfaceY = baseY + def.GetSurfaceHeight(Settings.BlockPixelSize);
            return new SurfaceColumnInfo(blockCoord, block, def, layer, baseY, surfaceY);
        }

        return new SurfaceColumnInfo(blockCoord, Block.Air, null, 0, 0f, 0f);
    }

    public float GetSurfaceTopY(int worldX, int worldZ)
    {
        var info = GetSurfaceColumnInfo(worldX, worldZ);
        return info.HasSurface ? info.SurfaceY : 0f;
    }

    public float GetDecorationTopY(int worldX, int worldZ)
    {
        var info = GetSurfaceColumnInfo(worldX, worldZ);
        if (!info.HasSurface)
            return 0f;

        return info.BaseY + info.Def.GetDecorationHeight(Settings.BlockPixelSize);
    }

    public bool IsColumnOccluder(int worldX, int worldZ)
    {
        return GetSurfaceColumnInfo(worldX, worldZ).IsOccluder;
    }

    public Vector3 GetBlockCenterWorld(int worldX, int worldZ, float surfaceY = 0f)
    {
        float half = Settings.BlockPixelSize * 0.5f;
        return new Vector3(
            worldX * Settings.BlockPixelSize + half,
            surfaceY,
            worldZ * Settings.BlockPixelSize + half);
    }

    public void RefreshViewDependentVisuals()
    {
        foreach (var chunk in _loadedChunks.Values)
            chunk.IsDirty = true;
    }

    private void LoadChunk(Vector2I chunkCoord)
    {
        var chunk = new Chunk(chunkCoord);
        bool restoredFromCache = _chunkCache != null && _chunkCache.TryRestore(chunkCoord, chunk);
        if (!restoredFromCache)
            _generator.GenerateChunk(chunk);

        _loadedChunks[chunkCoord] = chunk;

        var renderer = new ChunkRenderer();
        renderer.SetChunk(chunk);
        renderer.Position = chunk.WorldPosition3D;
        AddChild(renderer);
        _renderers[chunkCoord] = renderer;
    }

    private void UnloadDistantChunks(Vector2I centerChunkCoord)
    {
        var toRemove = new List<Vector2I>();

        foreach (var coord in _loadedChunks.Keys)
        {
            if (ChunkDistance(coord, centerChunkCoord) > Settings.UnloadRadius)
                toRemove.Add(coord);
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

    private void RebuildLoadQueue(Vector2I center)
    {
        _loadQueue.Clear();

        var coords = new List<Vector2I>();
        int r = Settings.LoadRadius;

        for (int dz = -r; dz <= r; dz++)
        {
            for (int dx = -r; dx <= r; dx++)
                coords.Add(new Vector2I(center.X + dx, center.Y + dz));
        }

        coords.Sort((a, b) =>
        {
            float distA = (a - center).LengthSquared();
            float distB = (b - center).LengthSquared();
            return distA.CompareTo(distB);
        });

        foreach (var coord in coords.Where(coord => !_loadedChunks.ContainsKey(coord)))
            _loadQueue.Enqueue(coord);
    }

    public static Vector2I WorldToChunkCoord(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.X / Settings.ChunkPixelSize);
        int cz = Mathf.FloorToInt(worldPos.Z / Settings.ChunkPixelSize);
        return new Vector2I(cx, cz);
    }

    public static Vector2I BlockToChunkCoord(int worldX, int worldZ)
    {
        int cx = worldX >= 0 ? worldX / Settings.ChunkSize : (worldX - Settings.ChunkSize + 1) / Settings.ChunkSize;
        int cz = worldZ >= 0 ? worldZ / Settings.ChunkSize : (worldZ - Settings.ChunkSize + 1) / Settings.ChunkSize;
        return new Vector2I(cx, cz);
    }

    public static Vector2I BlockToLocalCoord(int worldX, int worldZ)
    {
        int lx = ((worldX % Settings.ChunkSize) + Settings.ChunkSize) % Settings.ChunkSize;
        int lz = ((worldZ % Settings.ChunkSize) + Settings.ChunkSize) % Settings.ChunkSize;
        return new Vector2I(lx, lz);
    }

    public Block GetBlock(int worldX, int worldZ, int layer = 0)
    {
        var chunkCoord = BlockToChunkCoord(worldX, worldZ);
        if (!_loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return Block.Air;

        var local = BlockToLocalCoord(worldX, worldZ);
        return chunk.GetBlock(local.X, local.Y, layer);
    }

    public void SetBlock(int worldX, int worldZ, Block block, int layer = 0)
    {
        var chunkCoord = BlockToChunkCoord(worldX, worldZ);
        if (!_loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return;

        var local = BlockToLocalCoord(worldX, worldZ);
        chunk.SetBlock(local.X, local.Y, block, layer);
    }

    private static int ChunkDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }

    private static Vector3 IntersectRayWithHorizontalPlane(Vector3 from, Vector3 dir, float y)
    {
        if (Mathf.Abs(dir.Y) < 0.0001f)
            return new Vector3(from.X, y, from.Z);

        float t = (y - from.Y) / dir.Y;
        return from + dir * t;
    }
}
