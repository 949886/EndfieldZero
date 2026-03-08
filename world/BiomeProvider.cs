namespace EndfieldZero.World;

/// <summary>
/// Biome type enum. Organized by climate zones like Minecraft's biome system.
/// Selection based on 4 noise parameters: Temperature, Humidity, Continentalness, Erosion.
/// </summary>
public enum BiomeType
{
    // --- 海洋 ---
    DeepOcean,          // 深海
    Ocean,              // 海洋
    WarmOcean,          // 暖水海洋
    FrozenOcean,        // 冻洋

    // --- 过渡 ---
    Beach,              // 沙滩
    SnowyBeach,         // 雪地沙滩
    StonyShore,         // 石岸

    // --- 温带 ---
    Plains,             // 平原
    SunflowerPlains,    // 向日葵平原（花多）
    Forest,             // 森林
    BirchForest,        // 白桦林
    DarkForest,         // 黑森林
    FlowerForest,       // 繁花森林
    Meadow,             // 草甸

    // --- 热带 ---
    Savanna,            // 稀树草原
    SavannaPlateau,     // 稀树高原
    Desert,             // 沙漠
    Badlands,           // 恶地（陶瓦）
    Jungle,             // 丛林
    SparseJungle,       // 稀疏丛林

    // --- 寒带 ---
    Taiga,              // 针叶林
    SnowyTaiga,         // 雪地针叶林
    SnowyPlains,        // 雪原
    IceSpikes,          // 冰刺平原
    FrozenRiver,        // 冰河

    // --- 特殊 ---
    Swamp,              // 沼泽
    MangroveSwamp,      // 红树林沼泽
    MushroomIsland,     // 蘑菇岛
    WindsweptHills,     // 风袭丘陵
    Mountain,           // 山脉
    SnowyMountain,      // 雪山

    // --- 河流 ---
    River,              // 河流
}

/// <summary>
/// Minecraft-inspired biome selection using 4 climate parameters:
///   Temperature  [-1, 1]: cold → hot
///   Humidity     [-1, 1]: dry → wet
///   Continentalness [-1, 1]: ocean → inland
///   Erosion      [-1, 1]: flat → mountainous
///
/// The parameter space is divided into bands, and each band combination maps
/// to a specific biome, creating natural transitions.
/// </summary>
public sealed class BiomeProvider
{
    /// <summary>
    /// Select biome from 4 climate parameters. This is the core selection logic
    /// modeled after Minecraft's multi-noise biome source.
    /// </summary>
    public BiomeType GetBiome(float temperature, float humidity,
                               float continentalness, float erosion)
    {
        // === Step 1: Ocean vs Land ===
        if (continentalness < -0.45f)
        {
            return GetOceanBiome(temperature, continentalness);
        }

        // === Step 2: Coast / Beach ===
        if (continentalness < -0.2f)
        {
            return GetCoastBiome(temperature, erosion);
        }

        // === Step 3: River check (narrow band in erosion space) ===
        // Rivers form in valleys (low erosion areas near specific continentalness)
        if (erosion < -0.55f && continentalness > -0.1f && continentalness < 0.6f)
        {
            if (temperature < -0.4f)
                return BiomeType.FrozenRiver;
            return BiomeType.River;
        }

        // === Step 4: Mountain ===
        if (erosion > 0.55f || continentalness > 0.75f)
        {
            return GetMountainBiome(temperature, humidity, erosion);
        }

        // === Step 5: Inland biomes by temperature/humidity ===
        return GetInlandBiome(temperature, humidity, continentalness, erosion);
    }

    /// <summary>Select ocean biome variant.</summary>
    private static BiomeType GetOceanBiome(float temperature, float continentalness)
    {
        bool isDeep = continentalness < -0.7f;

        if (temperature < -0.4f)
            return isDeep ? BiomeType.FrozenOcean : BiomeType.FrozenOcean;
        if (temperature > 0.4f)
            return isDeep ? BiomeType.WarmOcean : BiomeType.WarmOcean;

        return isDeep ? BiomeType.DeepOcean : BiomeType.Ocean;
    }

    /// <summary>Select coastal biome.</summary>
    private static BiomeType GetCoastBiome(float temperature, float erosion)
    {
        if (erosion > 0.4f)
            return BiomeType.StonyShore;
        if (temperature < -0.4f)
            return BiomeType.SnowyBeach;
        return BiomeType.Beach;
    }

    /// <summary>Select mountain biome.</summary>
    private static BiomeType GetMountainBiome(float temperature, float humidity, float erosion)
    {
        if (temperature < -0.3f)
            return BiomeType.SnowyMountain;
        if (erosion > 0.75f)
            return BiomeType.Mountain;
        return BiomeType.WindsweptHills;
    }

    /// <summary>
    /// Select inland biome — the main table.
    /// Temperature bands: Frozen / Cold / Temperate / Warm / Hot
    /// Humidity bands: Arid / Dry / Neutral / Wet / Humid
    /// </summary>
    private static BiomeType GetInlandBiome(float temperature, float humidity,
                                             float continentalness, float erosion)
    {
        // Temperature band
        int tempBand = GetBand(temperature, -0.45f, -0.15f, 0.2f, 0.5f); // 0-4
        // Humidity band
        int humBand = GetBand(humidity, -0.4f, -0.1f, 0.15f, 0.45f);    // 0-4

        // Biome lookup table [temperature][humidity]
        // Rows: Frozen(0), Cold(1), Temperate(2), Warm(3), Hot(4)
        // Cols: Arid(0), Dry(1), Neutral(2), Wet(3), Humid(4)
        return (tempBand, humBand) switch
        {
            // === Frozen ===
            (0, 0) => BiomeType.IceSpikes,
            (0, 1) => BiomeType.SnowyPlains,
            (0, 2) => BiomeType.SnowyPlains,
            (0, 3) => BiomeType.SnowyTaiga,
            (0, 4) => BiomeType.SnowyTaiga,

            // === Cold ===
            (1, 0) => BiomeType.SnowyPlains,
            (1, 1) => BiomeType.Plains,
            (1, 2) => BiomeType.Taiga,
            (1, 3) => BiomeType.Taiga,
            (1, 4) => BiomeType.DarkForest,

            // === Temperate ===
            (2, 0) => BiomeType.Meadow,
            (2, 1) => BiomeType.Plains,
            (2, 2) => BiomeType.Forest,
            (2, 3) => BiomeType.BirchForest,
            (2, 4) => BiomeType.DarkForest,

            // === Warm ===
            (3, 0) => BiomeType.Savanna,
            (3, 1) => erosion > 0.3f ? BiomeType.SavannaPlateau : BiomeType.SunflowerPlains,
            (3, 2) => BiomeType.FlowerForest,
            (3, 3) => BiomeType.Forest,
            (3, 4) => humidity > 0.55f ? BiomeType.Swamp : BiomeType.SparseJungle,

            // === Hot ===
            (4, 0) => BiomeType.Desert,
            (4, 1) => continentalness > 0.5f ? BiomeType.Badlands : BiomeType.Desert,
            (4, 2) => BiomeType.Savanna,
            (4, 3) => BiomeType.SparseJungle,
            (4, 4) => BiomeType.Jungle,

            _ => BiomeType.Plains,
        };
    }

    /// <summary>Quantize a [-1,1] value into 5 bands (0-4).</summary>
    private static int GetBand(float value, float t1, float t2, float t3, float t4)
    {
        if (value < t1) return 0;
        if (value < t2) return 1;
        if (value < t3) return 2;
        if (value < t4) return 3;
        return 4;
    }

    // ===== Per-Biome Data Lookups =====

    /// <summary>Get the primary ground block for a biome.</summary>
    public ushort GetGroundBlock(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.DeepOcean       => BlockRegistry.DeepWaterId,
            BiomeType.Ocean           => BlockRegistry.WaterId,
            BiomeType.WarmOcean       => BlockRegistry.WaterId,
            BiomeType.FrozenOcean     => BlockRegistry.PackedIceId,

            BiomeType.Beach           => BlockRegistry.SandId,
            BiomeType.SnowyBeach      => BlockRegistry.SandId,
            BiomeType.StonyShore      => BlockRegistry.GravelId,

            BiomeType.Plains          => BlockRegistry.GrassId,
            BiomeType.SunflowerPlains => BlockRegistry.GrassId,
            BiomeType.Meadow          => BlockRegistry.GrassId,
            BiomeType.Forest          => BlockRegistry.GrassId,
            BiomeType.BirchForest     => BlockRegistry.GrassId,
            BiomeType.DarkForest      => BlockRegistry.DarkGrassId,
            BiomeType.FlowerForest    => BlockRegistry.GrassId,

            BiomeType.Savanna         => BlockRegistry.SavannaGrassId,
            BiomeType.SavannaPlateau  => BlockRegistry.CoarseDirtId,
            BiomeType.Desert          => BlockRegistry.SandId,
            BiomeType.Badlands        => BlockRegistry.TerracottaId,
            BiomeType.Jungle          => BlockRegistry.JungleGrassId,
            BiomeType.SparseJungle    => BlockRegistry.JungleGrassId,

            BiomeType.Taiga           => BlockRegistry.PodzolId,
            BiomeType.SnowyTaiga      => BlockRegistry.PodzolId,
            BiomeType.SnowyPlains     => BlockRegistry.SnowId,
            BiomeType.IceSpikes       => BlockRegistry.IceId,
            BiomeType.FrozenRiver     => BlockRegistry.IceId,

            BiomeType.Swamp           => BlockRegistry.MudId,
            BiomeType.MangroveSwamp   => BlockRegistry.MudId,
            BiomeType.MushroomIsland  => BlockRegistry.MyceliumId,
            BiomeType.WindsweptHills  => BlockRegistry.GravelId,
            BiomeType.Mountain        => BlockRegistry.StoneId,
            BiomeType.SnowyMountain   => BlockRegistry.SnowId,

            BiomeType.River           => BlockRegistry.RiverId,

            _ => BlockRegistry.GrassId,
        };
    }

    /// <summary>Get primary tree type for a biome (AirId = no trees).</summary>
    public ushort GetTreeBlock(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest          => BlockRegistry.TreeId,
            BiomeType.BirchForest     => BlockRegistry.BirchTreeId,
            BiomeType.DarkForest      => BlockRegistry.TreeId,
            BiomeType.FlowerForest    => BlockRegistry.BirchTreeId,
            BiomeType.Taiga           => BlockRegistry.ConiferTreeId,
            BiomeType.SnowyTaiga      => BlockRegistry.ConiferTreeId,
            BiomeType.SparseJungle    => BlockRegistry.JungleTreeId,
            BiomeType.Jungle          => BlockRegistry.JungleTreeId,
            BiomeType.Savanna         => BlockRegistry.AcaciaTreeId,
            BiomeType.Swamp           => BlockRegistry.TreeId,
            BiomeType.MangroveSwamp   => BlockRegistry.TreeId,
            BiomeType.Plains          => BlockRegistry.TreeId,       // rare
            BiomeType.SunflowerPlains => BlockRegistry.TreeId,       // rare
            BiomeType.Meadow          => BlockRegistry.BirchTreeId, // rare
            _ => BlockRegistry.AirId,     // no trees
        };
    }

    /// <summary>Get tree density for a biome (0.0 to 1.0).</summary>
    public float GetTreeDensity(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Jungle          => 0.30f,
            BiomeType.DarkForest      => 0.25f,
            BiomeType.Forest          => 0.18f,
            BiomeType.BirchForest     => 0.15f,
            BiomeType.FlowerForest    => 0.12f,
            BiomeType.Taiga           => 0.14f,
            BiomeType.SnowyTaiga      => 0.12f,
            BiomeType.SparseJungle    => 0.08f,
            BiomeType.Swamp           => 0.10f,
            BiomeType.MangroveSwamp   => 0.15f,
            BiomeType.Savanna         => 0.02f,
            BiomeType.SavannaPlateau  => 0.01f,
            BiomeType.Plains          => 0.008f,
            BiomeType.SunflowerPlains => 0.005f,
            BiomeType.Meadow          => 0.005f,
            BiomeType.WindsweptHills  => 0.02f,
            _ => 0f,
        };
    }

    /// <summary>Get vegetation block (grass, flowers, cactus, etc.). AirId = none.</summary>
    public ushort GetVegetationBlock(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Plains          => BlockRegistry.TallGrassId,
            BiomeType.SunflowerPlains => BlockRegistry.FlowerId,
            BiomeType.FlowerForest    => BlockRegistry.FlowerId,
            BiomeType.Meadow          => BlockRegistry.FlowerId,
            BiomeType.Desert          => BlockRegistry.CactusId,
            BiomeType.Badlands        => BlockRegistry.DeadBushId,
            BiomeType.Savanna         => BlockRegistry.TallGrassId,
            BiomeType.Jungle          => BlockRegistry.TallGrassId,
            BiomeType.SparseJungle    => BlockRegistry.TallGrassId,
            BiomeType.Swamp           => BlockRegistry.MushroomId,
            BiomeType.MushroomIsland  => BlockRegistry.MushroomId,
            BiomeType.Taiga           => BlockRegistry.MushroomId,
            _ => BlockRegistry.AirId,
        };
    }

    /// <summary>Get vegetation density for a biome (0.0 to 1.0).</summary>
    public float GetVegetationDensity(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.FlowerForest    => 0.15f,
            BiomeType.SunflowerPlains => 0.10f,
            BiomeType.Meadow          => 0.12f,
            BiomeType.Plains          => 0.04f,
            BiomeType.Desert          => 0.005f,
            BiomeType.Badlands        => 0.01f,
            BiomeType.Savanna         => 0.03f,
            BiomeType.Jungle          => 0.06f,
            BiomeType.SparseJungle    => 0.04f,
            BiomeType.Swamp           => 0.05f,
            BiomeType.MushroomIsland  => 0.20f,
            BiomeType.Taiga           => 0.03f,
            _ => 0f,
        };
    }

    /// <summary>Get ore density multiplier for a biome.</summary>
    public float GetOreDensity(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Mountain       => 0.10f,
            BiomeType.SnowyMountain  => 0.08f,
            BiomeType.WindsweptHills => 0.06f,
            BiomeType.Badlands       => 0.07f,
            BiomeType.StonyShore     => 0.04f,
            _ => 0.01f,
        };
    }
}
