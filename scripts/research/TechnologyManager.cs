using System;
using System.Collections.Generic;
using System.Linq;
using Cherry.Building;
using Cherry.Core;
using Cherry.Jobs;
using Cherry.World;
using Godot;

namespace Cherry.Research;

public partial class TechnologyManager : Node
{
    private const string SavePath = TechnologyTreePaths.ProgressSavePath;
    private const string SaveSection = "technology";
    private const long ResearchMaintenanceIntervalTicks = 30;
    private const string TechnologyResourcePath = TechnologyTreePaths.DefaultResourcePath;

    private readonly List<TechnologyDef> _definitions = new();
    private readonly Dictionary<string, TechnologyDef> _defsById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _initialUnlockedBuildings = new(StringComparer.Ordinal)
    {
        "wall_wood",
        "door_wood",
        "floor_wood",
        "torch",
        "research_desk",
    };

    private string _activeTechnologyId = "";
    private float _activeProgressTicks;
    private int _activeResearchJobId = -1;
    private bool _hasResearchDesk;
    private long _lastMaintenanceTick = long.MinValue;

    [Export] public TechnologyTreeDef TechnologyTree { get; set; }

    public static TechnologyManager Instance { get; private set; }

    public event Action Changed;

    public IEnumerable<TechnologyDef> AllTechnologies => _definitions;
    public string ActiveTechnologyId => _activeTechnologyId;
    public float ActiveProgressTicks => _activeProgressTicks;
    public bool HasResearchDesk
    {
        get
        {
            RefreshResearchDeskAvailability();
            return _hasResearchDesk;
        }
    }
    public bool HasActiveResearch => !string.IsNullOrEmpty(_activeTechnologyId) && !IsCompleted(_activeTechnologyId);

    public float ConstructionSpeedMultiplier => 1f + SumCompletedModifier(TechnologyEffectType.ConstructionSpeedMultiplier);
    public float ColonyMoveSpeedMultiplier => 1f + SumCompletedModifier(TechnologyEffectType.ColonyMoveSpeedMultiplier);

    public override void _Ready()
    {
        Instance = this;
        BuildTechnologyDefinitions();
        LoadProgress();
        RecomputeResearchDeskAvailability();
        EventBus.Tick += OnTick;
        MaintainResearchJob();
    }

    public override void _ExitTree()
    {
        EventBus.Tick -= OnTick;
        if (Instance == this)
            Instance = null;
    }

    public TechnologyDef GetTechnology(string technologyId)
        => string.IsNullOrWhiteSpace(technologyId) ? null : _defsById.GetValueOrDefault(technologyId);

    public TechnologyState GetState(string technologyId)
    {
        var tech = GetTechnology(technologyId);
        if (tech == null)
            return TechnologyState.Locked;

        if (_completedIds.Contains(technologyId))
            return TechnologyState.Completed;

        if (string.Equals(_activeTechnologyId, technologyId, StringComparison.Ordinal))
            return TechnologyState.InProgress;

        return PrerequisitesMet(tech)
            ? TechnologyState.Available
            : TechnologyState.Locked;
    }

    public bool IsCompleted(string technologyId)
        => !string.IsNullOrWhiteSpace(technologyId) && _completedIds.Contains(technologyId);

    public bool IsBuildingUnlocked(string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId))
            return false;

        if (_initialUnlockedBuildings.Contains(buildingId))
            return true;

        foreach (string completedId in _completedIds)
        {
            var tech = GetTechnology(completedId);
            if (tech == null)
                continue;

            foreach (TechnologyEffectDef effect in tech.Effects)
            {
                if (effect == null || effect.EffectType != TechnologyEffectType.UnlockBuilding)
                    continue;

                if (string.Equals(effect.TargetId, buildingId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    public bool CanStartResearch(string technologyId, out string reason)
    {
        RefreshResearchDeskAvailability();
        reason = "";
        var tech = GetTechnology(technologyId);
        if (tech == null)
        {
            reason = "未知科技";
            return false;
        }

        if (IsCompleted(technologyId))
        {
            reason = "已完成";
            return false;
        }

        if (!PrerequisitesMet(tech))
        {
            reason = "前置未满足";
            return false;
        }

        if (HasActiveResearch && !string.Equals(_activeTechnologyId, technologyId, StringComparison.Ordinal))
        {
            reason = "已有研究项目";
            return false;
        }

        if (!_hasResearchDesk)
        {
            reason = "需要研究台";
            return false;
        }

        return true;
    }

    public bool StartResearch(string technologyId)
    {
        if (!CanStartResearch(technologyId, out _))
            return false;

        _activeTechnologyId = technologyId;
        _activeProgressTicks = Mathf.Clamp(_activeProgressTicks, 0f, GetTechnology(technologyId)?.ResearchTicks ?? 0f);
        _activeResearchJobId = -1;
        SaveProgress();
        EmitChanged();
        MaintainResearchJob();
        return true;
    }

    public float GetProgressRatio(string technologyId)
    {
        var tech = GetTechnology(technologyId);
        if (tech == null || tech.ResearchTicks <= 0)
            return 0f;

        if (IsCompleted(technologyId))
            return 1f;

        if (!string.Equals(_activeTechnologyId, technologyId, StringComparison.Ordinal))
            return 0f;

        return Mathf.Clamp(_activeProgressTicks / tech.ResearchTicks, 0f, 1f);
    }

    public float GetRemainingResearchTicks(string technologyId)
    {
        var tech = GetTechnology(technologyId);
        if (tech == null)
            return 0f;

        if (IsCompleted(technologyId))
            return 0f;

        if (!string.Equals(_activeTechnologyId, technologyId, StringComparison.Ordinal))
            return tech.ResearchTicks;

        return Mathf.Max(0f, tech.ResearchTicks - _activeProgressTicks);
    }

    public string DescribeEffects(TechnologyDef tech)
    {
        if (tech == null || tech.Effects.Count == 0)
            return "无";

        List<string> parts = new();
        foreach (TechnologyEffectDef effect in tech.Effects)
        {
            if (effect == null)
                continue;

            switch (effect.EffectType)
            {
                case TechnologyEffectType.UnlockBuilding:
                {
                    string display = BuildingRegistry.Instance.GetDef(effect.TargetId)?.DisplayName ?? effect.TargetId;
                    parts.Add($"解锁建筑: {display}");
                    break;
                }
                case TechnologyEffectType.ConstructionSpeedMultiplier:
                    parts.Add($"建造速度 +{effect.Value * 100f:F0}%");
                    break;
                case TechnologyEffectType.ColonyMoveSpeedMultiplier:
                    parts.Add($"殖民者移动速度 +{effect.Value * 100f:F0}%");
                    break;
            }
        }

        return parts.Count == 0 ? "无" : string.Join("  ", parts);
    }

    public string DescribePrerequisites(TechnologyDef tech)
    {
        if (tech == null || tech.PrerequisiteIds.Count == 0)
            return "无";

        List<string> names = new();
        foreach (string prereqId in tech.PrerequisiteIds)
        {
            names.Add(GetTechnology(prereqId)?.DisplayName ?? prereqId);
        }

        return string.Join("、", names);
    }

    public void OnResearchJobCompleted(int jobId, string technologyId, float workTicks)
    {
        if (jobId == _activeResearchJobId)
            _activeResearchJobId = -1;

        if (string.IsNullOrEmpty(technologyId) || !string.Equals(_activeTechnologyId, technologyId, StringComparison.Ordinal))
        {
            MaintainResearchJob();
            return;
        }

        var tech = GetTechnology(technologyId);
        if (tech == null)
            return;

        _activeProgressTicks = Mathf.Min(tech.ResearchTicks, _activeProgressTicks + Mathf.Max(workTicks, 0f));

        if (_activeProgressTicks >= tech.ResearchTicks)
        {
            CompleteTechnology(tech);
            return;
        }

        SaveProgress();
        EmitChanged();
        MaintainResearchJob();
    }

    private void OnTick(long currentTick)
    {
        if (currentTick - _lastMaintenanceTick < ResearchMaintenanceIntervalTicks)
            return;

        _lastMaintenanceTick = currentTick;
        bool hadDesk = _hasResearchDesk;
        RefreshResearchDeskAvailability();
        MaintainResearchJob();

        if (hadDesk != _hasResearchDesk)
            EmitChanged();
    }

    private void RecomputeResearchDeskAvailability()
    {
        _hasResearchDesk = FindResearchDesk() != null;
    }

    private bool RefreshResearchDeskAvailability()
    {
        bool hadDesk = _hasResearchDesk;
        RecomputeResearchDeskAvailability();
        return hadDesk != _hasResearchDesk;
    }

    private void MaintainResearchJob()
    {
        RefreshResearchDeskAvailability();

        if (!HasActiveResearch)
        {
            _activeResearchJobId = -1;
            return;
        }

        if (_activeResearchJobId >= 0)
        {
            var existingJob = JobSystem.Instance?.GetJob(_activeResearchJobId);
            if (existingJob == null ||
                existingJob.Status == JobStatus.Completed ||
                existingJob.Status == JobStatus.Failed)
            {
                _activeResearchJobId = -1;
            }
        }

        if (_activeResearchJobId >= 0 || !_hasResearchDesk || JobSystem.Instance == null)
            return;

        var technology = GetTechnology(_activeTechnologyId);
        var desk = FindResearchDesk();
        if (technology == null || desk == null)
            return;

        if (!TryFindInteractionCell(desk, out Vector2I interactionCell))
            return;

        Vector3 targetWorldPos = desk.GlobalPosition;
        float workTicks = Mathf.Clamp(GetRemainingResearchTicks(technology.Id) * 0.5f, 120f, 240f);
        var job = new Job("Research", $"研究 {technology.DisplayName}")
        {
            TargetBlockCoord = interactionCell,
            TargetWorldPos = targetWorldPos,
            RequiredSkill = "Intellect",
            MinSkillLevel = 0f,
            WorkTicks = Mathf.CeilToInt(workTicks),
            BasePriority = 6,
            XpPerTick = 0.65f,
            ResearchTechnologyId = technology.Id,
            ResearchDeskCell = desk.BlockCoord,
        };

        JobSystem.Instance.AddJob(job);
        _activeResearchJobId = job.Id;
    }

    private void CompleteTechnology(TechnologyDef technology)
    {
        _completedIds.Add(technology.Id);
        _activeTechnologyId = "";
        _activeProgressTicks = 0f;
        _activeResearchJobId = -1;
        SaveProgress();
        EmitChanged();
    }

    private bool PrerequisitesMet(TechnologyDef technology)
    {
        if (technology == null)
            return false;

        foreach (string prereqId in technology.PrerequisiteIds)
        {
            if (!_completedIds.Contains(prereqId))
                return false;
        }

        return true;
    }

    private float SumCompletedModifier(TechnologyEffectType effectType)
    {
        float total = 0f;
        foreach (string completedId in _completedIds)
        {
            var tech = GetTechnology(completedId);
            if (tech == null)
                continue;

            foreach (TechnologyEffectDef effect in tech.Effects)
            {
                if (effect != null && effect.EffectType == effectType)
                    total += effect.Value;
            }
        }

        return total;
    }

    private BuildingInstance FindResearchDesk()
    {
        var sceneRoot = GetTree().CurrentScene ?? GetTree().Root.GetChildOrNull<Node>(0);
        var container = sceneRoot?.GetNodeOrNull<Node3D>("EntityContainer");
        if (container == null)
            return null;

        foreach (Node child in container.GetChildren())
        {
            if (child is BuildingInstance building &&
                building.Def != null &&
                string.Equals(building.Def.Id, "research_desk", StringComparison.Ordinal))
            {
                return building;
            }
        }

        return null;
    }

    private bool TryFindInteractionCell(BuildingInstance building, out Vector2I interactionCell)
    {
        interactionCell = Vector2I.Zero;
        if (building == null || WorldManager.Instance == null)
            return false;

        HashSet<Vector2I> occupied = building.OccupiedCells().ToHashSet();
        Vector2I min = building.BlockCoord - Vector2I.One;
        Vector2I max = building.BlockCoord + building.EffectiveSize + Vector2I.One;

        for (int z = min.Y; z <= max.Y; z++)
        {
            for (int x = min.X; x <= max.X; x++)
            {
                var candidate = new Vector2I(x, z);
                if (occupied.Contains(candidate))
                    continue;

                bool adjacentToFootprint =
                    x >= building.BlockCoord.X - 1 &&
                    x <= building.BlockCoord.X + building.EffectiveSize.X &&
                    z >= building.BlockCoord.Y - 1 &&
                    z <= building.BlockCoord.Y + building.EffectiveSize.Y;
                if (!adjacentToFootprint)
                    continue;

                if (!IsWalkableCell(candidate))
                    continue;

                if (IsOtherBuildingOccupying(candidate, building))
                    continue;

                interactionCell = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsWalkableCell(Vector2I cell)
    {
        if (WorldManager.Instance == null)
            return false;

        Block block = WorldManager.Instance.GetBlock(cell.X, cell.Y);
        var def = BlockRegistry.Instance.GetDef(block.TypeId);
        if (def == null)
            return false;

        return !def.IsSolid && block.TypeId != BlockRegistry.WaterId && block.TypeId != BlockRegistry.RiverId;
    }

    private bool IsOtherBuildingOccupying(Vector2I cell, BuildingInstance ignored)
    {
        var sceneRoot = GetTree().CurrentScene ?? GetTree().Root.GetChildOrNull<Node>(0);
        var container = sceneRoot?.GetNodeOrNull<Node3D>("EntityContainer");
        if (container == null)
            return false;

        foreach (Node child in container.GetChildren())
        {
            if (child is not BuildingInstance building || building == ignored)
                continue;

            foreach (Vector2I occupied in building.OccupiedCells())
            {
                if (occupied == cell)
                    return true;
            }
        }

        return false;
    }

    private void BuildTechnologyDefinitions()
    {
        _definitions.Clear();
        _defsById.Clear();

        TechnologyTreeDef sourceTree = TechnologyTree;
        if (sourceTree == null && ResourceLoader.Exists(TechnologyResourcePath))
            sourceTree = TechnologyTreeUtils.LoadTreeResource(TechnologyResourcePath);

        if (sourceTree == null)
            GD.PushWarning($"Technology tree resource was not found. Expected assignment or fallback path '{TechnologyResourcePath}'.");

        TechnologyTreeDef tree = TechnologyTreeUtils.CloneTree(sourceTree ?? TechnologyTreeUtils.CreateEmptyTree());
        TechnologyTreeUtils.NormalizeTree(tree);

        foreach (TechnologyDef technology in tree.Technologies)
        {
            if (technology == null || string.IsNullOrWhiteSpace(technology.Id))
                continue;

            RegisterTechnology(technology);
        }

        return;
/*

        RegisterTechnology(new TechnologyDef
        {
            Id = "stone_working",
            DisplayName = "石工基础",
            Description = "学会基础石料加工与稳定砌筑，为更坚固的墙体和地板提供工艺支持。",
            ResearchTicks = 600,
            Icon = CreateSolidIcon(new Color(0.47f, 0.5f, 0.56f)),
            CanvasPosition = new Vector2(180f, 220f),
            Effects = new Godot.Collections.Array<TechnologyEffectDef>
            {
                TechnologyEffectDef.UnlockBuilding("wall_stone"),
                TechnologyEffectDef.UnlockBuilding("floor_stone"),
            },
        });

        RegisterTechnology(new TechnologyDef
        {
            Id = "furniture_design",
            DisplayName = "家具设计",
            Description = "整理居住空间的基本家具方案，提升殖民地的舒适度与实用性。",
            ResearchTicks = 700,
            Icon = CreateSolidIcon(new Color(0.72f, 0.53f, 0.32f)),
            CanvasPosition = new Vector2(180f, 470f),
            Effects = new Godot.Collections.Array<TechnologyEffectDef>
            {
                TechnologyEffectDef.UnlockBuilding("bed"),
                TechnologyEffectDef.UnlockBuilding("table"),
                TechnologyEffectDef.UnlockBuilding("chair"),
            },
        });

        RegisterTechnology(new TechnologyDef
        {
            Id = "tool_standardization",
            DisplayName = "工具标准化",
            Description = "统一常用工器具的规格与流程，让施工协作更加顺畅。",
            ResearchTicks = 800,
            Icon = CreateSolidIcon(new Color(0.32f, 0.65f, 0.88f)),
            CanvasPosition = new Vector2(620f, 470f),
            PrerequisiteIds = new Godot.Collections.Array<string> { "furniture_design" },
            Effects = new Godot.Collections.Array<TechnologyEffectDef>
            {
                TechnologyEffectDef.Modifier(TechnologyEffectType.ConstructionSpeedMultiplier, 0.20f),
            },
        });

        RegisterTechnology(new TechnologyDef
        {
            Id = "advanced_crafting",
            DisplayName = "进阶工坊",
            Description = "解锁更稳定的生产设施，让石工与基础制造能够持续运转。",
            ResearchTicks = 900,
            Icon = CreateSolidIcon(new Color(0.55f, 0.58f, 0.29f)),
            CanvasPosition = new Vector2(620f, 220f),
            PrerequisiteIds = new Godot.Collections.Array<string> { "stone_working" },
            Effects = new Godot.Collections.Array<TechnologyEffectDef>
            {
                TechnologyEffectDef.UnlockBuilding("workbench"),
                TechnologyEffectDef.UnlockBuilding("stove"),
            },
        });

        RegisterTechnology(new TechnologyDef
        {
            Id = "colony_logistics",
            DisplayName = "殖民物流",
            Description = "通过路径规划与物资分流提升殖民者日常行动效率。",
            ResearchTicks = 1000,
            Icon = CreateSolidIcon(new Color(0.41f, 0.79f, 0.66f)),
            CanvasPosition = new Vector2(1060f, 220f),
            PrerequisiteIds = new Godot.Collections.Array<string> { "advanced_crafting" },
            Effects = new Godot.Collections.Array<TechnologyEffectDef>
            {
                TechnologyEffectDef.Modifier(TechnologyEffectType.ColonyMoveSpeedMultiplier, 0.10f),
            },
        });
        RegisterTechnology(new TechnologyDef
        {
            Id = "modular_storage",
            DisplayName = "\u6A21\u5757\u5316\u4ED3\u50A8",
            Description = "\u5C06\u8FDB\u9636\u5DE5\u574A\u7684\u5236\u9020\u80FD\u529B\u5EF6\u4F38\u5230\u6807\u51C6\u5316\u5B58\u653E\u8BBE\u65BD\uFF0C\u89E3\u9501\u65B0\u7684\u50A8\u7269\u5EFA\u7B51\u4EE5\u652F\u6491\u66F4\u5927\u89C4\u6A21\u7684\u751F\u4EA7\u533A\u3002",
            ResearchTicks = 1000,
            Icon = CreateSolidIcon(new Color(0.74f, 0.58f, 0.32f)),
            CanvasPosition = new Vector2(1060f, 40f),
            PrerequisiteIds = new Godot.Collections.Array<string> { "advanced_crafting" },
            Effects = new Godot.Collections.Array<TechnologyEffectDef>
            {
                TechnologyEffectDef.UnlockBuilding("storage_shelf"),
            },
        });
*/
    }

    private void RegisterTechnology(TechnologyDef technology)
    {
        _definitions.Add(technology);
        _defsById[technology.Id] = technology;
    }

    private void LoadProgress()
    {
        _completedIds.Clear();
        _activeTechnologyId = "";
        _activeProgressTicks = 0f;
        _activeResearchJobId = -1;

        var config = new ConfigFile();
        if (config.Load(SavePath) != Error.Ok)
            return;

        var completedArray = (Godot.Collections.Array<string>)config.GetValue(
            SaveSection,
            "completed_ids",
            new Godot.Collections.Array<string>());
        foreach (string techId in completedArray)
        {
            if (_defsById.ContainsKey(techId))
                _completedIds.Add(techId);
        }

        string activeId = (string)config.GetValue(SaveSection, "active_id", "");
        float activeProgress = (float)(double)config.GetValue(SaveSection, "active_progress_ticks", 0.0);
        if (_defsById.ContainsKey(activeId) && !_completedIds.Contains(activeId))
        {
            _activeTechnologyId = activeId;
            _activeProgressTicks = Mathf.Clamp(activeProgress, 0f, _defsById[activeId].ResearchTicks);
        }
    }

    private void SaveProgress()
    {
        var config = new ConfigFile();
        var completedArray = new Godot.Collections.Array<string>();
        foreach (string techId in _completedIds.OrderBy(id => id, StringComparer.Ordinal))
            completedArray.Add(techId);

        config.SetValue(SaveSection, "completed_ids", completedArray);
        config.SetValue(SaveSection, "active_id", _activeTechnologyId);
        config.SetValue(SaveSection, "active_progress_ticks", _activeProgressTicks);
        config.Save(SavePath);
    }

    private void EmitChanged()
    {
        Changed?.Invoke();
    }

    private static Texture2D CreateSolidIcon(Color color)
    {
        var image = Image.CreateEmpty(72, 72, false, Image.Format.Rgba8);
        image.Fill(color);
        image.FillRect(new Rect2I(6, 6, 60, 60), color.Lightened(0.14f));
        image.FillRect(new Rect2I(10, 10, 52, 52), color.Darkened(0.08f));
        image.FillRect(new Rect2I(10, 56, 52, 6), color.Lightened(0.28f));
        return ImageTexture.CreateFromImage(image);
    }
}
