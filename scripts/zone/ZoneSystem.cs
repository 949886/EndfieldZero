using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Jobs;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Zone;

/// <summary>
/// Manages all player-defined zones. Handles creation, deletion, expansion,
/// and automatic job creation for Growing zones.
/// </summary>
public partial class ZoneSystem : Node
{
    private readonly List<Zone> _zones = new();

    public static ZoneSystem Instance { get; private set; }

    public IReadOnlyList<Zone> AllZones => _zones;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>Create a new zone of the given type covering the specified cells.</summary>
    public Zone CreateZone(string zoneType, IEnumerable<Vector2I> cells)
    {
        string displayName;
        Color color;

        switch (zoneType)
        {
            case "Stockpile":
                displayName = $"仓储区 #{_zones.Count(z => z.ZoneType == "Stockpile") + 1}";
                color = new Color(0.8f, 0.6f, 0.2f, 0.2f);
                break;
            case "Growing":
                displayName = $"种植区 #{_zones.Count(z => z.ZoneType == "Growing") + 1}";
                color = new Color(0.3f, 0.85f, 0.3f, 0.2f);
                break;
            case "Home":
                displayName = "居住区";
                color = new Color(0.3f, 0.5f, 1f, 0.15f);
                break;
            case "Dumping":
                displayName = $"垃圾区 #{_zones.Count(z => z.ZoneType == "Dumping") + 1}";
                color = new Color(0.6f, 0.4f, 0.2f, 0.2f);
                break;
            default:
                displayName = $"区划 #{_zones.Count + 1}";
                color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                break;
        }

        var zone = new Zone(zoneType, displayName, color);

        // Filter valid cells (walkable, not water/walls)
        var validCells = new List<Vector2I>();
        foreach (var cell in cells)
        {
            if (!IsCellValidForZone(cell, zoneType)) continue;
            if (IsCellInAnyZone(cell)) continue; // No overlapping zones

            validCells.Add(cell);
        }

        if (validCells.Count == 0) return null;

        zone.AddCells(validCells);
        _zones.Add(zone);

        // Auto-create grow jobs for Growing zones
        if (zoneType == "Growing")
        {
            CreateGrowJobs(zone);
        }

        GD.Print($"[Zone] Created {zone.DisplayName} ({zone.Cells.Count} cells)");
        return zone;
    }

    /// <summary>Delete a zone by ID.</summary>
    public void DeleteZone(int zoneId)
    {
        _zones.RemoveAll(z => z.Id == zoneId);
    }

    /// <summary>Delete zone(s) at a cell coordinate.</summary>
    public void DeleteZoneAt(Vector2I cell)
    {
        _zones.RemoveAll(z => z.ContainsCell(cell));
    }

    /// <summary>Get zone at a cell (if any).</summary>
    public Zone GetZoneAt(Vector2I cell) => _zones.FirstOrDefault(z => z.ContainsCell(cell));

    /// <summary>Check if a cell belongs to any zone.</summary>
    public bool IsCellInAnyZone(Vector2I cell) => _zones.Any(z => z.ContainsCell(cell));

    /// <summary>Validate a cell for a zone type.</summary>
    private bool IsCellValidForZone(Vector2I cell, string zoneType)
    {
        if (WorldManager.Instance == null) return false;
        var block = WorldManager.Instance.GetBlock(cell.X, cell.Y);
        var def = BlockRegistry.Instance.GetDef(block.TypeId);

        // Must be on non-solid, walkable terrain
        if (def == null) return false;
        if (def.IsSolid) return false;
        if (def.MoveSpeedMod <= 0f) return false;

        return true;
    }

    /// <summary>Create grow jobs for all cells in a Growing zone.</summary>
    private void CreateGrowJobs(Zone zone)
    {
        if (JobSystem.Instance == null) return;

        foreach (var cell in zone.Cells)
        {
            JobSystem.Instance.CreateGrowJob(cell.X, cell.Y);
        }
    }
}
