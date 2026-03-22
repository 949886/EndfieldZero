namespace EndfieldZero.Farming;

/// <summary>
/// Static definition for a crop type.
/// Maps to specific regions of the Farming Plants.png sprite sheet.
///
/// Sprite sheet layout: 16×16px tiles, 8 columns of crops.
/// Each crop has multiple growth stage sprites arranged vertically.
/// </summary>
public class CropDef
{
    /// <summary>Unique identifier. e.g. "wheat".</summary>
    public string Id { get; }

    /// <summary>Display name. e.g. "小麦".</summary>
    public string DisplayName { get; }

    /// <summary>Number of growth stages (corn=5, others=4).</summary>
    public int GrowthStages { get; }

    /// <summary>Game ticks per growth stage.</summary>
    public int TicksPerStage { get; }

    /// <summary>Total ticks from planting to maturity.</summary>
    public int TotalGrowthTicks => GrowthStages * TicksPerStage;

    /// <summary>Minimum Growing skill to plant.</summary>
    public float MinGrowingSkill { get; }

    /// <summary>Base harvest yield (items produced).</summary>
    public int HarvestYield { get; }

    /// <summary>XP per Grow/Harvest tick.</summary>
    public float XpPerTick { get; }

    // --- Sprite sheet mapping ---

    /// <summary>Column index in Farming Plants.png (0-based, left to right).</summary>
    public int SpriteColumn { get; }

    /// <summary>Starting row index for this crop's sprites (0-based, top to bottom).</summary>
    public int SpriteRowStart { get; }

    /// <summary>Pixel size of each tile in the sprite sheet.</summary>
    public const int TileSize = 16;

    public CropDef(
        string id, string displayName,
        int growthStages, int ticksPerStage,
        float minGrowingSkill, int harvestYield, float xpPerTick,
        int spriteColumn, int spriteRowStart)
    {
        Id = id;
        DisplayName = displayName;
        GrowthStages = growthStages;
        TicksPerStage = ticksPerStage;
        MinGrowingSkill = minGrowingSkill;
        HarvestYield = harvestYield;
        XpPerTick = xpPerTick;
        SpriteColumn = spriteColumn;
        SpriteRowStart = spriteRowStart;
    }
}
