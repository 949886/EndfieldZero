using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Procedural world generator using 4 climate noise layers matching
/// Minecraft's multi-noise biome source:
///   1. Temperature  — cold vs hot
///   2. Humidity     — dry vs wet
///   3. Continentalness — ocean vs inland
///   4. Erosion      — flat vs mountainous
/// Plus detail/ore noises for resource placement.
///
/// Deterministic: same seed + chunkCoord = same output.
/// </summary>
public sealed class WorldGenerator
{
    private readonly FastNoiseLite _temperatureNoise;
    private readonly FastNoiseLite _humidityNoise;
    private readonly FastNoiseLite _continentalnessNoise;
    private readonly FastNoiseLite _erosionNoise;
    private readonly FastNoiseLite _oreNoise;
    private readonly FastNoiseLite _detailNoise;
    private readonly FastNoiseLite _vegetationNoise;
    private readonly BiomeProvider _biomeProvider;
    private readonly int _seed;

    /// <summary>Controls biome size. Higher = larger biomes. Default 3.0.</summary>
    public float BiomeScale { get; }

    /// <summary>Fractal octaves for biome noise. Fewer = smoother. Default 2.</summary>
    public int BiomeOctaves { get; }

    /// <summary>Continent-level scale. Higher = larger landmasses. Default 5.0.</summary>
    public float ContinentScale { get; }

    // --- Base frequencies (before scaling) ---
    private const float BaseTempFreq = 0.004f;
    private const float BaseHumidFreq = 0.0035f;
    private const float BaseContinentFreq = 0.002f;
    private const float BaseErosionFreq = 0.006f;

    public WorldGenerator(int seed, float biomeScale = 3.0f, int biomeOctaves = 2,
                          float continentScale = 5.0f)
    {
        _seed = seed;
        BiomeScale = Mathf.Max(biomeScale, 0.1f);
        BiomeOctaves = Mathf.Clamp(biomeOctaves, 1, 6);
        ContinentScale = Mathf.Max(continentScale, 1.0f);
        _biomeProvider = new BiomeProvider();

        // Temperature — gradual north-to-south like real climate
        _temperatureNoise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = BiomeOctaves,
            Frequency = BaseTempFreq / BiomeScale,
            FractalLacunarity = 2.0f,
            FractalGain = 0.35f,
        };

        // Humidity — perpendicular variation to temperature
        _humidityNoise = new FastNoiseLite
        {
            Seed = seed + 1000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = BiomeOctaves,
            Frequency = BaseHumidFreq / BiomeScale,
            FractalLacunarity = 2.0f,
            FractalGain = 0.35f,
        };

        // Continentalness — very large features (ocean vs land)
        _continentalnessNoise = new FastNoiseLite
        {
            Seed = seed + 2000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = Mathf.Max(1, BiomeOctaves - 1),
            Frequency = BaseContinentFreq / ContinentScale,
            FractalLacunarity = 2.0f,
            FractalGain = 0.3f,
        };

        // Erosion — terrain roughness / elevation variation
        _erosionNoise = new FastNoiseLite
        {
            Seed = seed + 3000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = BiomeOctaves + 1,
            Frequency = BaseErosionFreq / BiomeScale,
            FractalLacunarity = 2.0f,
            FractalGain = 0.4f,
        };

        // Ore vein noise — clustered deposits
        _oreNoise = new FastNoiseLite
        {
            Seed = seed + 4000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            Frequency = 0.04f,
            CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean,
            CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance,
        };

        // Detail noise — small-scale scatter (trees, vegetation)
        _detailNoise = new FastNoiseLite
        {
            Seed = seed + 5000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 2,
            Frequency = 0.1f,
        };

        // Vegetation noise — separate from detail for independent scatter
        _vegetationNoise = new FastNoiseLite
        {
            Seed = seed + 6000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 2,
            Frequency = 0.15f,
        };
    }

    /// <summary>
    /// Generate terrain for a chunk. Fills chunk.Blocks on layer 0.
    /// Deterministic: same seed + chunkCoord = same result.
    /// </summary>
    public void GenerateChunk(Chunk chunk)
    {
        var origin = chunk.WorldOrigin;

        for (int lz = 0; lz < Settings.ChunkSize; lz++)
        {
            for (int lx = 0; lx < Settings.ChunkSize; lx++)
            {
                int worldX = origin.X + lx;
                int worldZ = origin.Y + lz;

                // Sample 4 climate parameters
                float temperature = _temperatureNoise.GetNoise2D(worldX, worldZ);
                float humidity = _humidityNoise.GetNoise2D(worldX, worldZ);
                float continentalness = _continentalnessNoise.GetNoise2D(worldX, worldZ);
                float erosion = _erosionNoise.GetNoise2D(worldX, worldZ);

                // Select biome
                BiomeType biome = _biomeProvider.GetBiome(temperature, humidity, continentalness, erosion);

                // Get ground block
                ushort blockType = _biomeProvider.GetGroundBlock(biome);

                // --- Feature placement ---
                // Trees
                float treeDensity = _biomeProvider.GetTreeDensity(biome);
                if (treeDensity > 0f)
                {
                    float treeNoise = (_detailNoise.GetNoise2D(worldX, worldZ) + 1f) * 0.5f;
                    if (treeNoise < treeDensity)
                    {
                        ushort treeBlock = _biomeProvider.GetTreeBlock(biome);
                        if (treeBlock != BlockRegistry.AirId)
                            blockType = treeBlock;
                    }
                }

                // Vegetation (flowers, tall grass, cactus, etc.)
                // Only place if not already a tree
                if (blockType == _biomeProvider.GetGroundBlock(biome))
                {
                    float vegDensity = _biomeProvider.GetVegetationDensity(biome);
                    if (vegDensity > 0f)
                    {
                        float vegNoise = (_vegetationNoise.GetNoise2D(worldX, worldZ) + 1f) * 0.5f;
                        if (vegNoise < vegDensity)
                        {
                            ushort vegBlock = _biomeProvider.GetVegetationBlock(biome);
                            if (vegBlock != BlockRegistry.AirId)
                                blockType = vegBlock;
                        }
                    }
                }

                // Ore placement (only in stone/mountain biomes)
                if (blockType == BlockRegistry.StoneId || blockType == BlockRegistry.GravelId)
                {
                    float oreDensity = _biomeProvider.GetOreDensity(biome);
                    float oreValue = _oreNoise.GetNoise2D(worldX, worldZ);
                    if (oreValue < -0.65f && oreDensity > 0.03f)
                    {
                        blockType = SelectOreType(worldX, worldZ);
                    }
                }

                chunk.SetBlock(lx, lz, new Block(blockType));
            }
        }

        chunk.IsGenerated = true;
        chunk.IsDirty = true;
    }

    /// <summary>Select ore type based on detail noise (weighted distribution).</summary>
    private ushort SelectOreType(int worldX, int worldZ)
    {
        float n = (_detailNoise.GetNoise2D(worldX + 500, worldZ + 500) + 1f) * 0.5f;

        // Distribution: Coal 40%, Iron 25%, Copper 20%, Gold 10%, Diamond 5%
        if (n < 0.40f) return BlockRegistry.OreCoalId;
        if (n < 0.65f) return BlockRegistry.OreIronId;
        if (n < 0.85f) return BlockRegistry.OreCopperId;
        if (n < 0.95f) return BlockRegistry.OreGoldId;
        return BlockRegistry.OreDiamondId;
    }
}
