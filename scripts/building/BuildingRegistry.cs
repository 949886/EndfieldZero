using System.Collections.Generic;
using System.Linq;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Building;

/// <summary>
/// Registry of all building definitions. Singleton pattern.
/// Provides lookup by ID and filtered listing by category.
///
/// Building categories:
///   Structure  — walls, doors, floors
///   Furniture  — beds, tables, chairs, torches
///   Production — workbenches, stoves, research desks
/// </summary>
public sealed class BuildingRegistry
{
    private readonly Dictionary<string, BuildingDef> _defs = new();
    private static BuildingRegistry _instance;

    public static BuildingRegistry Instance => _instance ??= CreateDefault();

    /// <summary>Get building definition by ID.</summary>
    public BuildingDef GetDef(string id) => _defs.GetValueOrDefault(id);

    /// <summary>Get all definitions.</summary>
    public IEnumerable<BuildingDef> AllDefs => _defs.Values;

    /// <summary>Get all definitions in a category.</summary>
    public IEnumerable<BuildingDef> GetByCategory(string category)
        => _defs.Values.Where(d => d.Category == category);

    /// <summary>Get all unique category names.</summary>
    public IEnumerable<string> Categories
        => _defs.Values.Select(d => d.Category).Distinct();

    /// <summary>Register a building definition.</summary>
    public void Register(BuildingDef def)
    {
        _defs[def.Id] = def;
    }

    // ------------------------------------------------------------------
    //  Default registry with all built-in buildings
    // ------------------------------------------------------------------
    private static BuildingRegistry CreateDefault()
    {
        var reg = new BuildingRegistry();

        // ===== Structure =====

        reg.Register(new BuildingDef(
            "wall_stone", "石墙", "Structure",
            size: new Vector2I(1, 1),
            placedBlockId: BlockRegistry.StoneWallId,
            blocksMovement: true,
            workTicks: 360,
            ghostColor: new Color(0.5f, 0.5f, 0.6f, 0.5f),
            materials: new() { { "Stone", 5 } }
        ));

        reg.Register(new BuildingDef(
            "wall_wood", "木墙", "Structure",
            size: new Vector2I(1, 1),
            placedBlockId: BlockRegistry.WoodWallId,
            blocksMovement: true,
            workTicks: 240,
            ghostColor: new Color(0.6f, 0.45f, 0.3f, 0.5f),
            materials: new() { { "Wood", 5 } }
        ));

        reg.Register(new BuildingDef(
            "door_wood", "木门", "Structure",
            size: new Vector2I(1, 1),
            placedBlockId: BlockRegistry.WoodDoorId,
            blocksMovement: false,  // Pawns can walk through doors
            workTicks: 300,
            ghostColor: new Color(0.6f, 0.4f, 0.25f, 0.5f),
            materials: new() { { "Wood", 3 } }
        ));

        reg.Register(new BuildingDef(
            "floor_stone", "石地板", "Structure",
            size: new Vector2I(1, 1),
            placedBlockId: BlockRegistry.StoneFloorId,
            blocksMovement: false,
            workTicks: 120,
            ghostColor: new Color(0.6f, 0.6f, 0.65f, 0.4f),
            materials: new() { { "Stone", 2 } },
            beautyOffset: 1f
        ));

        reg.Register(new BuildingDef(
            "floor_wood", "木地板", "Structure",
            size: new Vector2I(1, 1),
            placedBlockId: BlockRegistry.WoodFloorId,
            blocksMovement: false,
            workTicks: 90,
            ghostColor: new Color(0.55f, 0.4f, 0.25f, 0.4f),
            materials: new() { { "Wood", 2 } },
            beautyOffset: 2f, comfortOffset: 1f
        ));

        // ===== Furniture =====

        reg.Register(new BuildingDef(
            "bed", "床", "Furniture",
            size: new Vector2I(1, 2),
            placedBlockId: 0,  // Furniture doesn't change blocks
            blocksMovement: true, canRotate: true,
            workTicks: 420,
            ghostColor: new Color(0.4f, 0.4f, 0.7f, 0.5f),
            materials: new() { { "Wood", 8 } },
            satisfiesNeed: "Rest",
            comfortOffset: 15f,
            view3DStyle: BuildingView3DStyle.Bed
        ));

        reg.Register(new BuildingDef(
            "table", "桌子", "Furniture",
            size: new Vector2I(2, 1),
            placedBlockId: 0,
            blocksMovement: true, canRotate: true,
            workTicks: 300,
            ghostColor: new Color(0.5f, 0.4f, 0.3f, 0.5f),
            materials: new() { { "Wood", 6 } },
            beautyOffset: 2f,
            view3DStyle: BuildingView3DStyle.Table
        ));

        reg.Register(new BuildingDef(
            "chair", "椅子", "Furniture",
            size: new Vector2I(1, 1),
            placedBlockId: 0,
            blocksMovement: false, canRotate: true,
            workTicks: 150,
            ghostColor: new Color(0.5f, 0.4f, 0.3f, 0.5f),
            materials: new() { { "Wood", 3 } },
            comfortOffset: 5f,
            view3DStyle: BuildingView3DStyle.SolidBlock,
            visualHeight: 0.7f
        ));

        reg.Register(new BuildingDef(
            "torch", "火把", "Furniture",
            size: new Vector2I(1, 1),
            placedBlockId: 0,
            blocksMovement: false,
            workTicks: 60,
            ghostColor: new Color(1f, 0.8f, 0.3f, 0.5f),
            materials: new() { { "Wood", 1 } },
            beautyOffset: 3f,
            view3DStyle: BuildingView3DStyle.SolidBlock,
            visualHeight: 1.0f
        ));

        // ===== Production =====

        reg.Register(new BuildingDef(
            "workbench", "工作台", "Production",
            size: new Vector2I(2, 1),
            placedBlockId: 0,
            blocksMovement: true, canRotate: true,
            workTicks: 480,
            requiredSkill: "Construction", minSkillLevel: 3f,
            ghostColor: new Color(0.5f, 0.5f, 0.3f, 0.5f),
            materials: new() { { "Wood", 10 }, { "Stone", 5 } },
            view3DStyle: BuildingView3DStyle.Workstation
        ));

        reg.Register(new BuildingDef(
            "stove", "炉灶", "Production",
            size: new Vector2I(2, 1),
            placedBlockId: 0,
            blocksMovement: true, canRotate: true,
            workTicks: 540,
            requiredSkill: "Construction", minSkillLevel: 4f,
            ghostColor: new Color(0.7f, 0.3f, 0.2f, 0.5f),
            materials: new() { { "Stone", 15 } },
            view3DStyle: BuildingView3DStyle.Stove
        ));

        reg.Register(new BuildingDef(
            "research_desk", "研究台", "Production",
            size: new Vector2I(2, 1),
            placedBlockId: 0,
            blocksMovement: true, canRotate: true,
            workTicks: 600,
            requiredSkill: "Construction", minSkillLevel: 5f,
            ghostColor: new Color(0.3f, 0.5f, 0.7f, 0.5f),
            materials: new() { { "Wood", 10 }, { "Stone", 10 } },
            view3DStyle: BuildingView3DStyle.Workstation
        ));

        return reg;
    }
}
