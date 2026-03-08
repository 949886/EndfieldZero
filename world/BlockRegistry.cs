using System.Collections.Generic;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Registry of all block type definitions. Singleton pattern — initialize once at startup.
/// Provides O(1) lookup by block type ID.
/// </summary>
public sealed class BlockRegistry
{
    // --- Built-in type IDs ---
    public const ushort AirId = 0;
    public const ushort GrassId = 1;
    public const ushort DirtId = 2;
    public const ushort StoneId = 3;
    public const ushort WaterId = 4;
    public const ushort SandId = 5;
    public const ushort OreIronId = 6;
    public const ushort OreGoldId = 7;
    public const ushort TreeId = 8;
    public const ushort WallId = 9;
    public const ushort SnowId = 10;
    public const ushort MudId = 11;
    public const ushort DeepWaterId = 12;

    private readonly List<BlockDef> _defs = new();
    private static BlockRegistry _instance;

    public static BlockRegistry Instance => _instance ??= CreateDefault();

    /// <summary>Get block definition by type ID. Returns null if not found.</summary>
    public BlockDef GetDef(ushort typeId)
    {
        return typeId < _defs.Count ? _defs[typeId] : null;
    }

    /// <summary>Register a new block definition. ID must match its position in the list.</summary>
    public void Register(BlockDef def)
    {
        while (_defs.Count <= def.Id)
            _defs.Add(null);
        _defs[def.Id] = def;
    }

    /// <summary>Total number of registered block types.</summary>
    public int Count => _defs.Count;

    /// <summary>Create registry with all built-in block types.</summary>
    private static BlockRegistry CreateDefault()
    {
        var reg = new BlockRegistry();

        // Color palette for placeholder rendering
        reg.Register(new BlockDef(AirId,       "Air",        Colors.Transparent, isSolid: false, isTransparent: true, moveSpeedMod: 1f));
        reg.Register(new BlockDef(GrassId,     "Grass",      new Color("4a8c3f"),  isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(DirtId,      "Dirt",       new Color("8b6d3f"),  isSolid: false, moveSpeedMod: 0.9f));
        reg.Register(new BlockDef(StoneId,     "Stone",      new Color("7a7a7a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(WaterId,     "Water",      new Color("3a7ecf"),  isSolid: false, isTransparent: true, moveSpeedMod: 0.3f));
        reg.Register(new BlockDef(SandId,      "Sand",       new Color("d4c47a"),  isSolid: false, moveSpeedMod: 0.8f));
        reg.Register(new BlockDef(OreIronId,   "Iron Ore",   new Color("a0522d"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(OreGoldId,   "Gold Ore",   new Color("daa520"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(TreeId,      "Tree",       new Color("2d5a1e"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(WallId,      "Wall",       new Color("5a5a5a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(SnowId,      "Snow",       new Color("e8e8f0"),  isSolid: false, moveSpeedMod: 0.7f));
        reg.Register(new BlockDef(MudId,       "Mud",        new Color("5c4a2a"),  isSolid: false, moveSpeedMod: 0.5f));
        reg.Register(new BlockDef(DeepWaterId, "Deep Water", new Color("1a4e8f"),  isSolid: true,  moveSpeedMod: 0f));

        return reg;
    }
}
