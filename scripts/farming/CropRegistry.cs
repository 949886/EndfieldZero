using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EndfieldZero.Farming;

/// <summary>
/// Registry of all crop definitions.
/// </summary>
public sealed class CropRegistry
{
    private readonly Dictionary<string, CropDef> _defs = new();
    private static CropRegistry _instance;

    public static CropRegistry Instance => _instance ??= new CropRegistry();

    public CropDef GetDef(string id) => _defs.GetValueOrDefault(id);
    public IEnumerable<CropDef> AllDefs => _defs.Values;

    public void Register(CropDef def) => _defs[def.Id] = def;

    public static void Initialize(IEnumerable<CropDef> defs)
    {
        var reg = new CropRegistry();

        foreach (var def in defs ?? Enumerable.Empty<CropDef>())
        {
            if (def == null)
            {
                GD.PushWarning("[CropRegistry] Null crop definition was provided during initialization.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(def.Id))
            {
                GD.PushWarning("[CropRegistry] A crop definition is missing Id.");
                continue;
            }

            if (def.GrowthStages <= 0)
            {
                GD.PushWarning($"[CropRegistry] Crop def '{def.Id}' has no stage tiles.");
                continue;
            }

            reg.Register(def);
        }

        if (!reg._defs.Any())
            GD.PushWarning("[CropRegistry] No crop definitions were registered.");

        _instance = reg;
    }

    public CropDef GetRandom()
    {
        var all = _defs.Values.ToList();
        if (all.Count == 0)
        {
            GD.PushError("[CropRegistry] No crop definitions are registered.");
            return null;
        }

        return all[new Random().Next(all.Count)];
    }
}
