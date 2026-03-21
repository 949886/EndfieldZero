using System.Collections.Generic;
using Godot;

namespace EndfieldZero.Building;

/// <summary>
/// Static definition for a building type.
/// Describes what a building looks like, costs, and how it affects the world.
///
/// Examples:
///   "wall_stone"  — 1×1, blocks movement, places WallId block
///   "bed"         — 2×1, doesn't block, satisfies Rest need
///   "door_wood"   — 1×1, doesn't block movement (pawns can walk through)
/// </summary>
public class BuildingDef
{
    /// <summary>Unique identifier. e.g. "wall_stone".</summary>
    public string Id { get; }

    /// <summary>Display name. e.g. "石墙".</summary>
    public string DisplayName { get; }

    /// <summary>Category for menu grouping. e.g. "Structure", "Furniture", "Production".</summary>
    public string Category { get; }

    /// <summary>Size in blocks (Width × Depth on XZ). Most buildings are 1×1.</summary>
    public Vector2I Size { get; }

    /// <summary>Block TypeId placed in the world upon completion. 0 = no block change (furniture).</summary>
    public ushort PlacedBlockId { get; }

    /// <summary>Does this building block pawn movement?</summary>
    public bool BlocksMovement { get; }

    /// <summary>Can this building be rotated? (relevant for multi-tile buildings).</summary>
    public bool CanRotate { get; }

    /// <summary>Base work ticks to construct.</summary>
    public int WorkTicks { get; }

    /// <summary>Required skill name. e.g. "Construction".</summary>
    public string RequiredSkill { get; }

    /// <summary>Minimum skill level to construct.</summary>
    public float MinSkillLevel { get; }

    /// <summary>XP per work tick.</summary>
    public float XpPerTick { get; }

    /// <summary>Preview color for ghost overlay.</summary>
    public Color GhostColor { get; }

    /// <summary>
    /// Material requirements: key = material name, value = amount.
    /// Simplified: no hauling yet, just checked on placement.
    /// e.g. {"Stone": 5, "Wood": 2}
    /// </summary>
    public IReadOnlyDictionary<string, int> Materials { get; }

    /// <summary>Optional: what need does this satisfy? e.g. "Rest" for bed.</summary>
    public string SatisfiesNeed { get; }

    /// <summary>Optional: beauty modifier to surrounding area.</summary>
    public float BeautyOffset { get; }

    /// <summary>Optional: comfort modifier when using.</summary>
    public float ComfortOffset { get; }

    public BuildingDef(
        string id, string displayName, string category,
        Vector2I size, ushort placedBlockId,
        bool blocksMovement = true, bool canRotate = false,
        int workTicks = 300, string requiredSkill = "Construction",
        float minSkillLevel = 0f, float xpPerTick = 0.5f,
        Color? ghostColor = null,
        Dictionary<string, int> materials = null,
        string satisfiesNeed = null,
        float beautyOffset = 0f, float comfortOffset = 0f)
    {
        Id = id;
        DisplayName = displayName;
        Category = category;
        Size = size;
        PlacedBlockId = placedBlockId;
        BlocksMovement = blocksMovement;
        CanRotate = canRotate;
        WorkTicks = workTicks;
        RequiredSkill = requiredSkill;
        MinSkillLevel = minSkillLevel;
        XpPerTick = xpPerTick;
        GhostColor = ghostColor ?? new Color(0.3f, 0.5f, 1f, 0.5f);
        Materials = materials ?? new Dictionary<string, int>();
        SatisfiesNeed = satisfiesNeed;
        BeautyOffset = beautyOffset;
        ComfortOffset = comfortOffset;
    }
}
