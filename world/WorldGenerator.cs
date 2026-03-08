using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Procedural world generator. Given a seed and chunk coordinate, deterministically
/// generates terrain using Godot's FastNoiseLite. Stateless per-chunk — safe for
/// async or deferred generation.
///
/// Key parameters:
/// - BiomeScale: controls biome size (higher = larger biomes). Default 1.0.
/// - BiomeSpacing: controls distance between different biomes (higher = more spread).
/// </summary>
public sealed class WorldGenerator
{
    private readonly FastNoiseLite _elevationNoise;
    private readonly FastNoiseLite _moistureNoise;
    private readonly FastNoiseLite _continentNoise;  // Large-scale continent shape
    private readonly FastNoiseLite _oreNoise;
    private readonly FastNoiseLite _detailNoise;
    private readonly BiomeProvider _biomeProvider;
    private readonly int _seed;

    /// <summary>
    /// Controls the overall size of biomes.
    /// Higher = larger biomes. 1.0 = default, 3.0 = 3× larger biomes.
    /// Internally divides noise frequency by this value.
    /// </summary>
    public float BiomeScale { get; }

    /// <summary>
    /// Controls the number of fractal octaves for biome noise.
    /// Fewer octaves = smoother transitions, less fragmentation.
    /// Range: 1-6. Default: 2.
    /// </summary>
    public int BiomeOctaves { get; }

    /// <summary>
    /// Controls continent-level variation scale.
    /// Higher = very large landmass features (ocean vs land).
    /// Default: 5.0.
    /// </summary>
    public float ContinentScale { get; }

    /// <summary>Base elevation frequency before scaling.</summary>
    private const float BaseElevationFrequency = 0.005f;

    /// <summary>Base moisture frequency before scaling.</summary>
    private const float BaseMoistureFrequency = 0.003f;

    /// <summary>Continent noise frequency (very low for huge features).</summary>
    private const float BaseContinentFrequency = 0.001f;

    public WorldGenerator(int seed, float biomeScale = 3.0f, int biomeOctaves = 2,
                          float continentScale = 5.0f)
    {
        _seed = seed;
        BiomeScale = Mathf.Max(biomeScale, 0.1f);
        BiomeOctaves = Mathf.Clamp(biomeOctaves, 1, 6);
        ContinentScale = Mathf.Max(continentScale, 1.0f);
        _biomeProvider = new BiomeProvider();

        // Elevation noise — divide frequency by BiomeScale for larger biomes
        _elevationNoise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = BiomeOctaves,
            Frequency = BaseElevationFrequency / BiomeScale,
            FractalLacunarity = 2.0f,
            FractalGain = 0.4f,  // Lower gain = less high-frequency detail = smoother
        };

        // Moisture noise — also scaled for consistent biome regions
        _moistureNoise = new FastNoiseLite
        {
            Seed = seed + 1000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = BiomeOctaves,
            Frequency = BaseMoistureFrequency / BiomeScale,
            FractalLacunarity = 2.0f,
            FractalGain = 0.4f,
        };

        // Continent noise — very large-scale shapes (ocean vs land)
        _continentNoise = new FastNoiseLite
        {
            Seed = seed + 4000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 1,
            Frequency = BaseContinentFrequency / ContinentScale,
        };

        // Ore vein noise — clustered deposits (independent of biome scale)
        _oreNoise = new FastNoiseLite
        {
            Seed = seed + 2000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            Frequency = 0.04f,
            CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean,
            CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance,
        };

        // Detail noise — small-scale variation (trees, rocks scatter)
        _detailNoise = new FastNoiseLite
        {
            Seed = seed + 3000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 2,
            Frequency = 0.1f,
        };
    }

    /// <summary>
    /// Generate terrain for a chunk. Fills chunk.Blocks on layer 0.
    /// Deterministic: same seed + chunkCoord = same result.
    /// </summary>
    public void GenerateChunk(Chunk chunk)
    {
        var origin = chunk.WorldOrigin;

        for (int lz = 0; lz < Constants.ChunkSize; lz++)
        {
            for (int lx = 0; lx < Constants.ChunkSize; lx++)
            {
                int worldX = origin.X + lx;
                int worldZ = origin.Y + lz;

                // Continent noise adds large-scale elevation bias
                float continent = _continentNoise.GetNoise2D(worldX, worldZ);

                // Combine continent + elevation for final height
                // continent influence: 40% continent, 60% local elevation
                float localElevation = _elevationNoise.GetNoise2D(worldX, worldZ);
                float elevation = continent * 0.4f + localElevation * 0.6f;

                float moisture = _moistureNoise.GetNoise2D(worldX, worldZ);

                BiomeType biome = _biomeProvider.GetBiome(elevation, moisture);
                ushort groundBlock = _biomeProvider.GetGroundBlock(biome);

                // Start with ground block
                ushort blockType = groundBlock;

                // Water at low elevation
                if (elevation < -0.15f)
                {
                    blockType = elevation < -0.35f ? BlockRegistry.DeepWaterId : BlockRegistry.WaterId;
                }
                else
                {
                    // Tree placement
                    float treeDensity = _biomeProvider.GetTreeDensity(biome);
                    if (treeDensity > 0f)
                    {
                        float treeNoise = (_detailNoise.GetNoise2D(worldX, worldZ) + 1f) * 0.5f;
                        if (treeNoise < treeDensity)
                        {
                            blockType = BlockRegistry.TreeId;
                        }
                    }

                    // Ore placement (only in mountain/stone areas)
                    if (blockType == BlockRegistry.StoneId)
                    {
                        float oreValue = _oreNoise.GetNoise2D(worldX, worldZ);
                        if (oreValue < -0.7f)
                        {
                            // Use detail noise to decide iron vs gold
                            float oreTypeNoise = _detailNoise.GetNoise2D(worldX + 500, worldZ + 500);
                            blockType = oreTypeNoise > 0.3f ? BlockRegistry.OreGoldId : BlockRegistry.OreIronId;
                        }
                    }
                }

                chunk.SetBlock(lx, lz, new Block(blockType));
            }
        }

        chunk.IsGenerated = true;
        chunk.IsDirty = true;
    }
}
