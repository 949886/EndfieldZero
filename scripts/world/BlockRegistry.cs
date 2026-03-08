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

    // --- New biome blocks ---
    public const ushort GravelId = 13;
    public const ushort RedSandId = 14;
    public const ushort DarkGrassId = 15;      // 深色草地（针叶林）
    public const ushort PodzolId = 16;          // 灰化土（针叶林地面）
    public const ushort MyceliumId = 17;        // 菌丝（蘑菇岛）
    public const ushort IceId = 18;             // 冰
    public const ushort PackedIceId = 19;       // 浮冰
    public const ushort CoarseDirtId = 20;      // 砂土（恶地/稀树草原）
    public const ushort TerracottaId = 21;      // 陶瓦（恶地）
    public const ushort SavannaGrassId = 22;    // 稀树草原草
    public const ushort JungleGrassId = 23;     // 丛林草
    public const ushort ConiferTreeId = 24;     // 针叶树
    public const ushort BirchTreeId = 25;       // 白桦树
    public const ushort JungleTreeId = 26;      // 丛林树
    public const ushort AcaciaTreeId = 27;      // 金合欢树
    public const ushort CactusId = 28;          // 仙人掌
    public const ushort DeadBushId = 29;        // 枯灌木
    public const ushort FlowerId = 30;          // 花
    public const ushort TallGrassId = 31;       // 高草
    public const ushort OreCoalId = 32;         // 煤矿
    public const ushort OreCopperId = 33;       // 铜矿
    public const ushort OreDiamondId = 34;      // 钻石矿
    public const ushort MushroomId = 35;        // 蘑菇
    public const ushort RiverId = 36;           // 河流（浅水变体）

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

        // --- 基础方块 ---
        reg.Register(new BlockDef(AirId,         "Air",          Colors.Transparent, isSolid: false, isTransparent: true, moveSpeedMod: 1f));
        reg.Register(new BlockDef(GrassId,       "Grass",        new Color("5b9b3e"),  isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(DirtId,        "Dirt",         new Color("8b6d3f"),  isSolid: false, moveSpeedMod: 0.9f));
        reg.Register(new BlockDef(StoneId,       "Stone",        new Color("7a7a7a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(WaterId,       "Water",        new Color("3a7ecf"),  isSolid: false, isTransparent: true, moveSpeedMod: 0.3f));
        reg.Register(new BlockDef(SandId,        "Sand",         new Color("dbd3a0"),  isSolid: false, moveSpeedMod: 0.8f));
        reg.Register(new BlockDef(OreIronId,     "Iron Ore",     new Color("a0522d"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(OreGoldId,     "Gold Ore",     new Color("daa520"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(TreeId,        "Oak Tree",     new Color("3d7a2e"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(WallId,        "Wall",         new Color("5a5a5a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(SnowId,        "Snow",         new Color("eaeef0"),  isSolid: false, moveSpeedMod: 0.7f));
        reg.Register(new BlockDef(MudId,         "Mud",          new Color("5c4a2a"),  isSolid: false, moveSpeedMod: 0.5f));
        reg.Register(new BlockDef(DeepWaterId,   "Deep Water",   new Color("1a4e8f"),  isSolid: true,  moveSpeedMod: 0f));

        // --- 新增方块 ---
        reg.Register(new BlockDef(GravelId,      "Gravel",       new Color("8c8680"),  isSolid: false, moveSpeedMod: 0.85f));
        reg.Register(new BlockDef(RedSandId,     "Red Sand",     new Color("ba6a3a"),  isSolid: false, moveSpeedMod: 0.8f));
        reg.Register(new BlockDef(DarkGrassId,   "Dark Grass",   new Color("3a6e2a"),  isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(PodzolId,      "Podzol",       new Color("6b5234"),  isSolid: false, moveSpeedMod: 0.9f));
        reg.Register(new BlockDef(MyceliumId,    "Mycelium",     new Color("8b7498"),  isSolid: false, moveSpeedMod: 0.9f));
        reg.Register(new BlockDef(IceId,         "Ice",          new Color("a0d4e8"),  isSolid: false, isTransparent: true, moveSpeedMod: 1.2f));
        reg.Register(new BlockDef(PackedIceId,   "Packed Ice",   new Color("7eb8d4"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(CoarseDirtId,  "Coarse Dirt",  new Color("7a5e30"),  isSolid: false, moveSpeedMod: 0.85f));
        reg.Register(new BlockDef(TerracottaId,  "Terracotta",   new Color("b86c42"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(SavannaGrassId,"Savanna Grass", new Color("9fa540"), isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(JungleGrassId, "Jungle Grass", new Color("4a9428"),  isSolid: false, moveSpeedMod: 0.9f));
        reg.Register(new BlockDef(ConiferTreeId, "Conifer Tree", new Color("1e4a1a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(BirchTreeId,   "Birch Tree",   new Color("7ab86a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(JungleTreeId,  "Jungle Tree",  new Color("2a6e1a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(AcaciaTreeId,  "Acacia Tree",  new Color("8a7a2a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(CactusId,      "Cactus",       new Color("5a8a2a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(DeadBushId,    "Dead Bush",    new Color("8a7a5a"),  isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(FlowerId,      "Flower",       new Color("d46a6a"),  isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(TallGrassId,   "Tall Grass",   new Color("6aaa4a"),  isSolid: false, moveSpeedMod: 0.95f));
        reg.Register(new BlockDef(OreCoalId,     "Coal Ore",     new Color("3a3a3a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(OreCopperId,   "Copper Ore",   new Color("b87a4a"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(OreDiamondId,  "Diamond Ore",  new Color("5ae8e8"),  isSolid: true,  moveSpeedMod: 0f));
        reg.Register(new BlockDef(MushroomId,    "Mushroom",     new Color("a03030"),  isSolid: false, moveSpeedMod: 1f));
        reg.Register(new BlockDef(RiverId,       "River",        new Color("4a90d0"),  isSolid: false, isTransparent: true, moveSpeedMod: 0.4f));

        return reg;
    }
}
