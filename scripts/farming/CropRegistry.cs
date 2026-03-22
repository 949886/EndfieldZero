using System.Collections.Generic;
using System.Linq;

namespace EndfieldZero.Farming;

/// <summary>
/// Registry of all crop definitions. Singleton pattern.
///
/// Farming Plants.png layout (16×16px tiles):
///   8 crops arranged in columns, each with growth stage rows.
///   Corn has 5 stages, all others have 4 stages.
///
///   Col 0: Wheat (小麦)          4 stages, rows 0-3
///   Col 1: Carrot (胡萝卜)       4 stages, rows 0-3
///   Col 2: Tomato (番茄)         4 stages, rows 0-3
///   Col 3: Pumpkin (南瓜)        4 stages, rows 0-3
///   Col 4: Sunflower (向日葵)    4 stages, rows 0-3
///   Col 5: Beetroot (甜菜)       4 stages, rows 0-3
///   Col 6: Corn (玉米)           5 stages, rows 0-4
///   Col 7: Blueberry (蓝莓)      4 stages, rows 0-3
/// </summary>
public sealed class CropRegistry
{
    private readonly Dictionary<string, CropDef> _defs = new();
    private static CropRegistry _instance;

    public static CropRegistry Instance => _instance ??= CreateDefault();

    public CropDef GetDef(string id) => _defs.GetValueOrDefault(id);
    public IEnumerable<CropDef> AllDefs => _defs.Values;

    public void Register(CropDef def) => _defs[def.Id] = def;

    private static CropRegistry CreateDefault()
    {
        var reg = new CropRegistry();

        //                          id           name    stages ticks skill yield  xp   col row
        reg.Register(new CropDef("wheat",       "小麦",   4,  600,  0f,   4,  0.3f,  0,  0));
        reg.Register(new CropDef("carrot",      "胡萝卜", 4,  480,  0f,   3,  0.3f,  1,  0));
        reg.Register(new CropDef("tomato",      "番茄",   4,  720,  2f,   5,  0.4f,  2,  0));
        reg.Register(new CropDef("pumpkin",     "南瓜",   4,  900,  3f,   2,  0.5f,  3,  0));
        reg.Register(new CropDef("sunflower",   "向日葵", 4,  600,  1f,   3,  0.3f,  4,  0));
        reg.Register(new CropDef("beetroot",    "甜菜",   4,  540,  1f,   4,  0.3f,  5,  0));
        reg.Register(new CropDef("corn",        "玉米",   5,  480,  2f,   6,  0.4f,  6,  0));
        reg.Register(new CropDef("blueberry",   "蓝莓",   4,  660,  3f,   8,  0.5f,  7,  0));

        return reg;
    }

    /// <summary>Get a random crop def for auto-planting.</summary>
    public CropDef GetRandom()
    {
        var all = _defs.Values.ToList();
        return all[new System.Random().Next(all.Count)];
    }
}
