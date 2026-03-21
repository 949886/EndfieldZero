using System;
using System.Collections.Generic;
using System.IO;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Keeps recently unloaded chunks in memory and persists modified chunks to disk.
/// Clean chunks may be evicted and regenerated; modified chunks are restored from
/// cache so player-built changes survive unload/reload and app restarts.
/// </summary>
public sealed class ChunkCache
{
    private const int FileMagic = 0x48435A45; // "EZCH"
    private const int FileVersion = 1;

    private sealed class CacheEntry
    {
        public Vector2I ChunkCoord { get; init; }
        public Block[] Blocks { get; init; }
        public bool IsModified { get; init; }
        public LinkedListNode<Vector2I> LruNode { get; set; }
    }

    private readonly Dictionary<Vector2I, CacheEntry> _memoryCache = new();
    private readonly LinkedList<Vector2I> _lru = new();
    private readonly string _cacheRoot;
    private readonly bool _persistModifiedChunks;
    private readonly int _maxMemoryChunks;

    public ChunkCache(int seed, int maxMemoryChunks = 256, bool persistModifiedChunks = true)
    {
        _persistModifiedChunks = persistModifiedChunks;
        _maxMemoryChunks = Mathf.Max(16, maxMemoryChunks);
        _cacheRoot = ProjectSettings.GlobalizePath($"user://chunk_cache/seed_{seed}");

        if (_persistModifiedChunks)
            Directory.CreateDirectory(_cacheRoot);
    }

    /// <summary>
    /// Try to restore a chunk from memory cache first, then disk-backed cache.
    /// Returns true when cached block data was applied.
    /// </summary>
    public bool TryRestore(Vector2I chunkCoord, Chunk chunk)
    {
        if (TryGetMemoryEntry(chunkCoord, out var memoryEntry))
        {
            Touch(memoryEntry);
            return chunk.TryLoadSnapshot(memoryEntry.Blocks, memoryEntry.IsModified);
        }

        if (!_persistModifiedChunks)
            return false;

        if (!TryReadFromDisk(chunkCoord, out var diskEntry))
            return false;

        AddOrReplaceMemoryEntry(diskEntry);
        return chunk.TryLoadSnapshot(diskEntry.Blocks, diskEntry.IsModified);
    }

    /// <summary>
    /// Store a snapshot of the chunk. Modified chunks are also written to disk.
    /// </summary>
    public void Store(Chunk chunk)
    {
        if (chunk == null)
            return;

        var entry = new CacheEntry
        {
            ChunkCoord = chunk.ChunkCoord,
            Blocks = chunk.CreateSnapshot(),
            IsModified = chunk.IsModified,
        };

        AddOrReplaceMemoryEntry(entry);

        if (entry.IsModified && _persistModifiedChunks)
            WriteToDisk(entry);
    }

    private bool TryGetMemoryEntry(Vector2I chunkCoord, out CacheEntry entry)
    {
        return _memoryCache.TryGetValue(chunkCoord, out entry);
    }

    private void AddOrReplaceMemoryEntry(CacheEntry entry)
    {
        if (_memoryCache.TryGetValue(entry.ChunkCoord, out var existing))
        {
            if (existing.LruNode != null)
                _lru.Remove(existing.LruNode);
        }

        entry.LruNode = _lru.AddLast(entry.ChunkCoord);
        _memoryCache[entry.ChunkCoord] = entry;
        EvictIfNeeded();
    }

    private void Touch(CacheEntry entry)
    {
        if (entry.LruNode == null || entry.LruNode.List != _lru)
        {
            entry.LruNode = _lru.AddLast(entry.ChunkCoord);
            return;
        }

        _lru.Remove(entry.LruNode);
        entry.LruNode = _lru.AddLast(entry.ChunkCoord);
    }

    private void EvictIfNeeded()
    {
        while (_memoryCache.Count > _maxMemoryChunks && _lru.First != null)
        {
            Vector2I oldestCoord = _lru.First.Value;
            _lru.RemoveFirst();
            _memoryCache.Remove(oldestCoord);
        }
    }

    private string GetChunkPath(Vector2I chunkCoord)
    {
        return Path.Combine(_cacheRoot, $"chunk_{chunkCoord.X}_{chunkCoord.Y}.bin");
    }

    private void WriteToDisk(CacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(_cacheRoot);

            using var stream = File.Open(GetChunkPath(entry.ChunkCoord), FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream);

            writer.Write(FileMagic);
            writer.Write(FileVersion);
            writer.Write(entry.ChunkCoord.X);
            writer.Write(entry.ChunkCoord.Y);
            writer.Write(Settings.ChunkSize);
            writer.Write(Settings.MaxLayers);
            writer.Write(entry.Blocks.Length);
            writer.Write(entry.IsModified);

            foreach (var block in entry.Blocks)
            {
                writer.Write(block.TypeId);
                writer.Write(block.Metadata);
                writer.Write(block.Layer);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ChunkCache] Failed to write chunk {entry.ChunkCoord}: {ex.Message}");
        }
    }

    private bool TryReadFromDisk(Vector2I chunkCoord, out CacheEntry entry)
    {
        entry = null;
        string path = GetChunkPath(chunkCoord);
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.Open(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            if (reader.ReadInt32() != FileMagic)
                return false;

            if (reader.ReadInt32() != FileVersion)
                return false;

            int storedX = reader.ReadInt32();
            int storedZ = reader.ReadInt32();
            int chunkSize = reader.ReadInt32();
            int maxLayers = reader.ReadInt32();
            int blockCount = reader.ReadInt32();
            bool isModified = reader.ReadBoolean();

            if (storedX != chunkCoord.X || storedZ != chunkCoord.Y)
                return false;

            int expectedBlocks = Settings.ChunkSize * Settings.ChunkSize * Settings.MaxLayers;
            if (chunkSize != Settings.ChunkSize || maxLayers != Settings.MaxLayers || blockCount != expectedBlocks)
                return false;

            var blocks = new Block[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                blocks[i] = new Block(reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte());
            }

            entry = new CacheEntry
            {
                ChunkCoord = chunkCoord,
                Blocks = blocks,
                IsModified = isModified,
            };
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ChunkCache] Failed to read chunk {chunkCoord}: {ex.Message}");
            return false;
        }
    }
}
