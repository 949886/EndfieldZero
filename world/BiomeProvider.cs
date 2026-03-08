using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Determines biome type based on elevation and moisture values.
/// Used by WorldGenerator to decide terrain composition per block.
/// </summary>
public enum BiomeType
{
    Ocean,
    Beach,
    Grassland,
    Forest,
    Desert,
    Mountain,
    Swamp,
    Tundra,
}

public sealed class BiomeProvider
{
    /// <summary>
    /// Select biome based on elevation [-1, 1] and moisture [-1, 1].
    /// </summary>
    public BiomeType GetBiome(float elevation, float moisture)
    {
        // Deep water
        if (elevation < -0.35f)
            return BiomeType.Ocean;

        // Shallow water / beach
        if (elevation < -0.15f)
            return BiomeType.Beach;

        // High elevation = mountain
        if (elevation > 0.65f)
            return BiomeType.Mountain;

        // Tundra at high elevation + low moisture
        if (elevation > 0.45f && moisture < -0.2f)
            return BiomeType.Tundra;

        // Mid-elevation biomes depend on moisture
        if (moisture < -0.3f)
            return BiomeType.Desert;

        if (moisture > 0.3f)
        {
            // High moisture + low elevation = swamp
            if (elevation < 0.05f)
                return BiomeType.Swamp;
            return BiomeType.Forest;
        }

        return BiomeType.Grassland;
    }

    /// <summary>Get the primary ground block for a biome.</summary>
    public ushort GetGroundBlock(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Ocean     => BlockRegistry.DeepWaterId,
            BiomeType.Beach     => BlockRegistry.SandId,
            BiomeType.Grassland => BlockRegistry.GrassId,
            BiomeType.Forest    => BlockRegistry.GrassId,
            BiomeType.Desert    => BlockRegistry.SandId,
            BiomeType.Mountain  => BlockRegistry.StoneId,
            BiomeType.Swamp     => BlockRegistry.MudId,
            BiomeType.Tundra    => BlockRegistry.SnowId,
            _                   => BlockRegistry.DirtId,
        };
    }

    /// <summary>Get tree density for a biome (0.0 to 1.0).</summary>
    public float GetTreeDensity(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Forest    => 0.15f,
            BiomeType.Grassland => 0.03f,
            BiomeType.Swamp     => 0.08f,
            BiomeType.Tundra    => 0.01f,
            _                   => 0f,
        };
    }

    /// <summary>Get ore density for a biome (0.0 to 1.0).</summary>
    public float GetOreDensity(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Mountain => 0.08f,
            BiomeType.Tundra   => 0.04f,
            _                  => 0.01f,
        };
    }
}
