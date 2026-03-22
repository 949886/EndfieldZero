using Godot;

namespace EndfieldZero.Items;

/// <summary>
/// Static definition for an item type.
/// Describes display info, stacking, sprite, and category.
///
/// Categories:
///   Resource — stone, wood, ores (from mining/woodcutting)
///   Food     — wheat, carrot, tomato, etc. (from harvesting crops)
///   Material — refined/special items
/// </summary>
public class ItemDef
{
    /// <summary>Unique identifier. e.g. "stone", "wheat".</summary>
    public string Id { get; }

    /// <summary>Display name. e.g. "石头".</summary>
    public string DisplayName { get; }

    /// <summary>Category for sorting/filtering. e.g. "Resource", "Food".</summary>
    public string Category { get; }

    /// <summary>Max stack size per tile.</summary>
    public int MaxStack { get; }

    /// <summary>Color for generated placeholder icon.</summary>
    public Color IconColor { get; }

    /// <summary>Nutrition value if edible (0 = not edible).</summary>
    public float NutritionValue { get; }

    /// <summary>Market/trade value per unit.</summary>
    public float BaseValue { get; }

    // --- Sprite (from Farming Plants items.png) ---
    /// <summary>Sprite column in item sheet (0-based). -1 = use generated icon.</summary>
    public int SpriteCol { get; }
    /// <summary>Sprite row in item sheet (0-based).</summary>
    public int SpriteRow { get; }
    public const int TileSize = 16;

    public ItemDef(
        string id, string displayName, string category,
        int maxStack = 75, Color? iconColor = null,
        float nutritionValue = 0f, float baseValue = 1f,
        int spriteCol = -1, int spriteRow = -1)
    {
        Id = id;
        DisplayName = displayName;
        Category = category;
        MaxStack = maxStack;
        IconColor = iconColor ?? new Color(0.6f, 0.6f, 0.6f);
        NutritionValue = nutritionValue;
        BaseValue = baseValue;
        SpriteCol = spriteCol;
        SpriteRow = spriteRow;
    }

    /// <summary>Whether this item uses a sprite sheet or generated icon.</summary>
    public bool HasSprite => SpriteCol >= 0;
}
