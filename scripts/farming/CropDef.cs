using Godot;
using Godot.Collections;

namespace EndfieldZero.Farming;

/// <summary>
/// Crop definition resource.
/// Growth stages map to explicit atlas textures in the crop sprite sheet.
/// </summary>
[GlobalClass]
public partial class CropDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int TicksPerStage { get; set; } = 600;
    [Export] public float MinGrowingSkill { get; set; }
    [Export] public int HarvestYield { get; set; } = 1;
    [Export] public float XpPerTick { get; set; } = 0.3f;
    [Export] public Array<AtlasTexture> StageTiles { get; set; } = new();

    public int GrowthStages => StageTiles.Count;
    public int TotalGrowthTicks => GrowthStages * TicksPerStage;
    
    public CropDef() {}

    public CropDef(
        string id,
        string displayName,
        int ticksPerStage,
        float minGrowingSkill,
        int harvestYield,
        float xpPerTick,
        Texture2D texture,
        int tileSize,
        params (int x, int y)[] stageTiles)
    {
        Id = id;
        DisplayName = displayName;
        TicksPerStage = ticksPerStage;
        MinGrowingSkill = minGrowingSkill;
        HarvestYield = harvestYield;
        XpPerTick = xpPerTick;
        StageTiles = BuildAtlasTextures(texture, tileSize, stageTiles);
    }


    public AtlasTexture GetStageTexture(int stage)
    {
        if (StageTiles.Count == 0)
            return null;

        int index = Mathf.Clamp(stage, 0, StageTiles.Count - 1);
        return StageTiles[index];
    }

    private static Array<AtlasTexture> BuildAtlasTextures(
        Texture2D texture,
        int tileSize,
        (int x, int y)[] stageTiles)
    {
        var textures = new Array<AtlasTexture>();

        if (texture == null || tileSize <= 0 || stageTiles == null)
            return textures;

        foreach (var (x, y) in stageTiles)
        {
            textures.Add(new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(x * tileSize, y * tileSize, tileSize, tileSize),
            });
        }

        return textures;
    }
}
