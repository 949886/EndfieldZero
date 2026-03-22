using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EndfieldZero.Farming;

/// <summary>
/// Manages all planted crop instances.
/// Provides lookup by block coordinate for harvest/removal.
/// </summary>
public partial class CropManager : Node
{
    private readonly Dictionary<Vector2I, CropInstance> _crops = new();

    public static CropManager Instance { get; private set; }

    public IReadOnlyDictionary<Vector2I, CropInstance> AllCrops => _crops;
    public int CropCount => _crops.Count;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>Plant a crop at the given block coordinate.</summary>
    public CropInstance PlantCrop(CropDef def, Vector2I blockCoord)
    {
        // Don't double-plant
        if (_crops.ContainsKey(blockCoord))
        {
            GD.Print($"[CropManager] Already planted at {blockCoord}");
            return null;
        }

        var instance = new CropInstance();
        instance.Init(def, blockCoord);

        // Add as child of the EntityContainer
        var container = GetTree().Root.GetChild(0)?.GetNodeOrNull<Node3D>("EntityContainer");
        if (container != null)
            container.AddChild(instance);
        else
            GetTree().Root.GetChild(0).AddChild(instance);

        _crops[blockCoord] = instance;
        GD.Print($"[CropManager] Planted {def.DisplayName} at {blockCoord}");
        return instance;
    }

    /// <summary>Remove a crop (harvested or destroyed).</summary>
    public void RemoveCrop(Vector2I blockCoord)
    {
        if (_crops.TryGetValue(blockCoord, out var crop))
        {
            crop.QueueFree();
            _crops.Remove(blockCoord);
        }
    }

    /// <summary>Get crop at coordinate.</summary>
    public CropInstance GetCropAt(Vector2I coord) => _crops.GetValueOrDefault(coord);

    /// <summary>Check if a cell has a crop.</summary>
    public bool HasCrop(Vector2I coord) => _crops.ContainsKey(coord);

    /// <summary>Get all mature crops.</summary>
    public IEnumerable<CropInstance> GetMatureCrops() => _crops.Values.Where(c => c.IsMature);
}
