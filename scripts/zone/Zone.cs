using System.Collections.Generic;
using Godot;

namespace EndfieldZero.Zone;

/// <summary>
/// Represents a player-defined zone — a set of cells with a specific purpose.
///
/// Zone types:
///   Stockpile — items can be stored here
///   Growing   — crops are planted here (auto-creates grow jobs)
///   Home      — defines the base area (affects beauty/comfort calculations)
///   Dumping   — unwanted items are hauled here
/// </summary>
public class Zone
{
    private static int _nextId = 1;

    public int Id { get; }
    public string ZoneType { get; }          // "Stockpile", "Growing", "Home", "Dumping"
    public string DisplayName { get; set; }
    public Color OverlayColor { get; }
    public HashSet<Vector2I> Cells { get; } = new();

    // Growing zone config
    public string CropType { get; set; } = "default";

    public Zone(string zoneType, string displayName, Color overlayColor)
    {
        Id = _nextId++;
        ZoneType = zoneType;
        DisplayName = displayName;
        OverlayColor = overlayColor;
    }

    public void AddCells(IEnumerable<Vector2I> cells)
    {
        foreach (var cell in cells)
            Cells.Add(cell);
    }

    public void RemoveCells(IEnumerable<Vector2I> cells)
    {
        foreach (var cell in cells)
            Cells.Remove(cell);
    }

    public bool ContainsCell(Vector2I cell) => Cells.Contains(cell);
}
