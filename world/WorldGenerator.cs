using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Procedural world generator. Given a seed and chunk coordinate, deterministically
/// generates terrain using Godot's FastNoiseLite. Stateless per-chunk — safe for
/// async or deferred generation.
/// </summary>
public sealed class WorldGenerator
{
    private readonly FastNoiseLite _elevationNoise;
    private readonly FastNoiseLite _moistureNoise;
    private readonly FastNoiseLite _oreNoise;
    private readonly FastNoiseLite _detailNoise;
    private readonly BiomeProvider _biomeProvider;
    private readonly int _seed;

    public WorldGenerator(int seed)
    {
        _seed = seed;
        _biomeProvider = new BiomeProvider();

        // Elevation noise — large-scale terrain shape
        _elevationNoise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 4,
            Frequency = 0.005f,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
        };

        // Moisture noise — biome variation
        _moistureNoise = new FastNoiseLite
        {
            Seed = seed + 1000,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 3,
            Frequency = 0.003f,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
        };

        // Ore vein noise — clustered deposits
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

                float elevation = _elevationNoise.GetNoise2D(worldX, worldZ);
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
