using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Pawn;
using Godot;

namespace EndfieldZero.Managers;

/// <summary>
/// Manages all pawns: spawning, tracking, and lifecycle.
/// Attach to the main scene and configure the pawn scene path.
/// Spawns initial colonists on _Ready.
/// </summary>
public partial class PawnManager : Node3D
{
    /// <summary>Path to the pawn.tscn scene.</summary>
    [Export] public PackedScene PawnScene { get; set; }

    /// <summary>Path to the enemy_pawn.tscn scene.</summary>
    [Export] public PackedScene EnemyPawnScene { get; set; }

    /// <summary>Number of initial colonists to spawn.</summary>
    [Export] public int InitialColonistCount { get; set; } = 3;

    /// <summary>Registry of active colonist pawns.</summary>
    private readonly Dictionary<int, Pawn.Pawn> _pawns = new();

    /// <summary>Registry of active enemy pawns.</summary>
    private readonly Dictionary<int, Pawn.EnemyPawn> _enemies = new();

    /// <summary>Next available pawn ID.</summary>
    private int _nextId = 1;

    // Name pools for random generation
    private static readonly string[] FirstNames = {
        "阿米娅", "凯尔希", "博士", "德克萨斯", "能天使",
        "拉普兰德", "银灰", "陈", "星熊", "推进之王",
        "伊芙利特", "艾雅法拉", "安洁莉娜", "蓝毒", "空",
        "闪灵", "夜莺", "塞雷娅", "赫默", "华法琳",
    };

    private static readonly string[] Nicknames = {
        "矿工", "建筑师", "园艺师", "厨师", "猎人",
        "医生", "工匠", "学者", "战士", "探险家",
    };

    /// <summary>Singleton access.</summary>
    public static PawnManager Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        if (PawnScene == null)
        {
            GD.PrintErr("[PawnManager] PawnScene not assigned! Cannot spawn colonists.");
            return;
        }

        // Spawn initial colonists
        for (int i = 0; i < InitialColonistCount; i++)
        {
            SpawnColonist(GetSpawnPosition(i));
        }

        GD.Print($"[PawnManager] Spawned {InitialColonistCount} colonists");
    }

    /// <summary>Spawn a new colonist at the given position.</summary>
    public Pawn.Pawn SpawnColonist(Vector3 position)
    {
        var instance = PawnScene.Instantiate<Pawn.Pawn>();
        var data = GenerateRandomPawnData();
        data.EquippedWeaponId = "rifle";
        instance.Data = data;
        instance.Position = position;
        AddChild(instance);

        _pawns[data.Id] = instance;
        EventBus.FirePawnSpawned(data.Id);
        return instance;
    }

    /// <summary>Get a pawn by ID.</summary>
    public Pawn.Pawn GetPawn(int id) => _pawns.GetValueOrDefault(id);

    /// <summary>Get all living pawns.</summary>
    public IEnumerable<Pawn.Pawn> GetAllPawns() => _pawns.Values;

    /// <summary>Generate a random PawnData for a new colonist.</summary>
    private PawnData GenerateRandomPawnData()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        var data = new PawnData
        {
            Id = _nextId++,
            PawnName = FirstNames[rng.RandiRange(0, FirstNames.Length - 1)],
            Nickname = Nicknames[rng.RandiRange(0, Nicknames.Length - 1)],
            Age = rng.RandiRange(18, 45),
            Gender = (Gender)rng.RandiRange(0, 1),
        };

        // Randomize stats (3-8 range, with some variation)
        data.Mining       = rng.RandiRange(2, 10);
        data.Construction = rng.RandiRange(2, 10);
        data.Growing      = rng.RandiRange(2, 10);
        data.Cooking      = rng.RandiRange(2, 10);
        data.Crafting     = rng.RandiRange(2, 10);
        data.Medical      = rng.RandiRange(2, 10);
        data.Social       = rng.RandiRange(2, 10);
        data.Artistic     = rng.RandiRange(2, 10);
        data.Shooting     = rng.RandiRange(2, 10);
        data.Strength     = rng.RandiRange(3, 8);
        data.Intellect    = rng.RandiRange(3, 8);
        data.Agility      = rng.RandiRange(3, 8);
        data.Will         = rng.RandiRange(3, 8);

        return data;
    }

    /// <summary>Get spawn position for initial colonists (clustered near origin).</summary>
    private Vector3 GetSpawnPosition(int index)
    {
        // Spawn in a small cluster around (0, 0, 0) with some offset
        float spacing = Settings.BlockPixelSize * 3; // 3 blocks apart
        float x = (index % 3 - 1) * spacing;
        float z = (index / 3) * spacing;
        return new Vector3(x, 0f, z);
    }

    /// <summary>Spawn a hostile enemy pawn (for raids/animal attacks).</summary>
    public Pawn.EnemyPawn SpawnHostile(Vector3 position, string faction = "Hostile",
        string weaponId = "", string name = null, HostileWaveContext waveContext = null)
    {
        if (EnemyPawnScene == null)
        {
            GD.PrintErr("[PawnManager] EnemyPawnScene not assigned!");
            return null;
        }

        var instance = EnemyPawnScene.Instantiate<Pawn.EnemyPawn>();
        var data = GenerateHostilePawnData();
        data.Faction = faction;
        data.EquippedWeaponId = weaponId;
        if (name != null) data.PawnName = name;
        instance.Data = data;
        instance.WaveContext = waveContext;
        instance.Position = position;
        AddChild(instance);

        _enemies[data.Id] = instance;
        return instance;
    }

    private PawnData GenerateHostilePawnData()
    {
        var data = GenerateRandomPawnData();
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        data.Shooting = rng.RandiRange(Settings.HostileShootingMin, Settings.HostileShootingMax);
        data.Strength = rng.RandiRange(Settings.HostileStrengthMin, Settings.HostileStrengthMax);
        data.Agility = rng.RandiRange(Settings.HostileAgilityMin, Settings.HostileAgilityMax);
        return data;
    }

    /// <summary>Spawn a neutral pawn (traders, wanderers).</summary>
    public Pawn.Pawn SpawnNeutral(Vector3 position, string name = null)
    {
        var instance = PawnScene.Instantiate<Pawn.Pawn>();
        var data = GenerateRandomPawnData();
        data.Faction = "Neutral";
        if (name != null) data.PawnName = name;
        instance.Data = data;
        instance.Position = position;
        AddChild(instance);

        _pawns[data.Id] = instance;
        return instance;
    }

    /// <summary>Get approximate center of the colony (average colonist position).</summary>
    public Vector3 GetColonyCenter()
    {
        Vector3 sum = Vector3.Zero;
        int count = 0;
        foreach (var pawn in _pawns.Values)
        {
            if (pawn.IsAlive && pawn.Data.Faction == "Colony")
            {
                sum += pawn.GlobalPosition;
                count++;
            }
        }
        return count > 0 ? sum / count : Vector3.Zero;
    }

    /// <summary>Count living colonists.</summary>
    public int GetColonistCount()
    {
        int count = 0;
        foreach (var pawn in _pawns.Values)
            if (pawn.IsAlive && pawn.Data.Faction == "Colony") count++;
        return count;
    }

    /// <summary>Get all living enemy pawns.</summary>
    public IEnumerable<Pawn.EnemyPawn> GetAllEnemies()
    {
        // Clean up dead/freed enemies
        var toRemove = new List<int>();
        foreach (var kv in _enemies)
        {
            if (!GodotObject.IsInstanceValid(kv.Value) || !kv.Value.IsAlive)
                toRemove.Add(kv.Key);
        }
        foreach (var id in toRemove)
            _enemies.Remove(id);

        return _enemies.Values;
    }

    /// <summary>Count living hostiles.</summary>
    public int GetHostileCount()
    {
        int count = 0;
        foreach (var enemy in GetAllEnemies())
            if (enemy.IsAlive) count++;
        return count;
    }
}
