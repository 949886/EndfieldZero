using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EndfieldZero.Items;

/// <summary>
/// Registry of all item definitions. Singleton pattern.
/// Provides lookup by ID and filtered listing.
///
/// Item sprite source: Farming Plants items.png (16×16 tiles, 2 columns)
/// Resource items use generated colored icons.
/// </summary>
public sealed class ItemRegistry
{
    private readonly Dictionary<string, ItemDef> _defs = new();
    private static ItemRegistry _instance;

    public static ItemRegistry Instance => _instance ??= CreateDefault();

    public ItemDef GetDef(string id) => _defs.GetValueOrDefault(id);
    public IEnumerable<ItemDef> AllDefs => _defs.Values;
    public IEnumerable<ItemDef> GetByCategory(string cat) => _defs.Values.Where(d => d.Category == cat);

    public void Register(ItemDef def) => _defs[def.Id] = def;

    private static ItemRegistry CreateDefault()
    {
        var reg = new ItemRegistry();

        // ===== Resources (generated icons) =====
        reg.Register(new ItemDef("stone",   "石头",   "Resource", maxStack: 75, iconColor: new Color("7a7a7a"), baseValue: 1f));
        reg.Register(new ItemDef("wood",    "木材",   "Resource", maxStack: 75, iconColor: new Color("8b6d3f"), baseValue: 1.5f));
        reg.Register(new ItemDef("iron",    "铁矿石", "Resource", maxStack: 50, iconColor: new Color("a0522d"), baseValue: 5f));
        reg.Register(new ItemDef("gold",    "金矿石", "Resource", maxStack: 50, iconColor: new Color("daa520"), baseValue: 15f));
        reg.Register(new ItemDef("copper",  "铜矿石", "Resource", maxStack: 50, iconColor: new Color("b87a4a"), baseValue: 4f));
        reg.Register(new ItemDef("coal",    "煤炭",   "Resource", maxStack: 75, iconColor: new Color("3a3a3a"), baseValue: 2f));
        reg.Register(new ItemDef("diamond", "钻石",   "Resource", maxStack: 25, iconColor: new Color("5ae8e8"), baseValue: 50f));
        reg.Register(new ItemDef("sand",    "沙子",   "Resource", maxStack: 75, iconColor: new Color("dbd3a0"), baseValue: 0.5f));

        // ===== Food (from Farming Plants items.png, col 0, rows 0-7) =====
        reg.Register(new ItemDef("wheat",     "小麦",   "Food", maxStack: 50, iconColor: new Color("d4a843"),
            nutritionValue: 10f, baseValue: 2f, spriteCol: 0, spriteRow: 0));
        reg.Register(new ItemDef("carrot",    "胡萝卜", "Food", maxStack: 50, iconColor: new Color("e87830"),
            nutritionValue: 12f, baseValue: 2f, spriteCol: 0, spriteRow: 1));
        reg.Register(new ItemDef("tomato",    "番茄",   "Food", maxStack: 50, iconColor: new Color("e03020"),
            nutritionValue: 14f, baseValue: 3f, spriteCol: 0, spriteRow: 2));
        reg.Register(new ItemDef("pumpkin",   "南瓜",   "Food", maxStack: 25, iconColor: new Color("e88020"),
            nutritionValue: 20f, baseValue: 5f, spriteCol: 0, spriteRow: 3));
        reg.Register(new ItemDef("sunflower_seed", "葵花籽", "Food", maxStack: 50, iconColor: new Color("f0d040"),
            nutritionValue: 8f, baseValue: 1.5f, spriteCol: 0, spriteRow: 4));
        reg.Register(new ItemDef("beetroot",  "甜菜",   "Food", maxStack: 50, iconColor: new Color("8a2050"),
            nutritionValue: 10f, baseValue: 2f, spriteCol: 0, spriteRow: 5));
        reg.Register(new ItemDef("corn",      "玉米",   "Food", maxStack: 50, iconColor: new Color("f0d860"),
            nutritionValue: 15f, baseValue: 3f, spriteCol: 0, spriteRow: 6));
        reg.Register(new ItemDef("blueberry", "蓝莓",   "Food", maxStack: 50, iconColor: new Color("4060d0"),
            nutritionValue: 8f, baseValue: 4f, spriteCol: 0, spriteRow: 7));

        // ===== Misc =====
        reg.Register(new ItemDef("mushroom", "蘑菇", "Food", maxStack: 50, iconColor: new Color("a03030"),
            nutritionValue: 6f, baseValue: 1f));

        return reg;
    }

    /// <summary>Map block type to dropped item ID. Returns null if block drops nothing.</summary>
    public static string BlockDropItemId(ushort blockTypeId)
    {
        return blockTypeId switch
        {
            World.BlockRegistry.StoneId => "stone",
            World.BlockRegistry.OreIronId => "iron",
            World.BlockRegistry.OreGoldId => "gold",
            World.BlockRegistry.OreCopperId => "copper",
            World.BlockRegistry.OreCoalId => "coal",
            World.BlockRegistry.OreDiamondId => "diamond",
            World.BlockRegistry.SandId => "sand",
            World.BlockRegistry.TreeId or
            World.BlockRegistry.ConiferTreeId or
            World.BlockRegistry.BirchTreeId or
            World.BlockRegistry.JungleTreeId or
            World.BlockRegistry.AcaciaTreeId => "wood",
            World.BlockRegistry.MushroomId => "mushroom",
            _ => null,
        };
    }

    /// <summary>Map crop ID to item ID.</summary>
    public static string CropDropItemId(string cropId)
    {
        return cropId switch
        {
            "wheat" => "wheat",
            "carrot" => "carrot",
            "tomato" => "tomato",
            "pumpkin" => "pumpkin",
            "sunflower" => "sunflower_seed",
            "beetroot" => "beetroot",
            "corn" => "corn",
            "blueberry" => "blueberry",
            _ => null,
        };
    }
}
