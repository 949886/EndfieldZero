using System;
using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Core;
using EndfieldZero.Items;
using EndfieldZero.Managers;
using EndfieldZero.Pawn;
using EndfieldZero.Storyteller.Incidents;
using Godot;

namespace EndfieldZero.Storyteller;

/// <summary>
/// AI Director — schedules and fires incidents based on colony wealth,
/// population, and game day. Inspired by RimWorld's storyteller system.
///
/// Difficulty curve:
///   Day 1-3:  protection period — only positive events
///   Day 4-10: small raids, weather
///   Day 11-30: medium raids + sieges, natural disasters
///   Day 31+:  large-scale raids, multi-direction attacks
/// </summary>
public partial class Storyteller : Node
{
    public static Storyteller Instance { get; private set; }

    private readonly Dictionary<string, int> _lastFiredDay = new();         // incidentId → last fire day
    private readonly Dictionary<string, IncidentWorker> _workers = new();
    private readonly List<IncidentDef> _allIncidents = new();

    private readonly Random _rng = new();
    private string _currentThreatLevel = "peace";

    public float ThreatPoints { get; private set; }
    public int CurrentDay => TimeManager.Instance?.CurrentDay ?? 1;
    public string ThreatLevel => _currentThreatLevel;

    public override void _Ready()
    {
        Instance = this;
        RegisterIncidents();
        EventBus.DayChanged += OnDayChanged;
    }

    public override void _ExitTree()
    {
        EventBus.DayChanged -= OnDayChanged;
    }

    private void RegisterIncidents()
    {
        // === Major Threats ===
        AddIncident(new IncidentDef("raid", "⚔️ 武装袭击", "敌对势力袭击了殖民地，准备战斗！", "MajorThreat",
            minThreatPoints: 200f, weight: 3f, cooldownDays: 3, minDayToTrigger: 4));
        AddIncident(new IncidentDef("animal_attack", "🐺 野兽攻击", "狂暴的动物袭击了殖民地，注意防范！", "MajorThreat",
            minThreatPoints: 100f, weight: 2f, cooldownDays: 5, minDayToTrigger: 4));

        // === Weather ===
        AddIncident(new IncidentDef("cold_snap", "❄️ 寒潮", "一次凛冽的寒潮降临了。气温急剧下降，作物受到严重威胁！", "Weather",
            weight: 1.5f, cooldownDays: 30, minDayToTrigger: 5, durationDays: 2f));
        AddIncident(new IncidentDef("heat_wave", "🔥 热浪", "致命的热浪来袭。气温升高，注意防暑！", "Weather",
            weight: 1.5f, cooldownDays: 30, minDayToTrigger: 5, durationDays: 2f));
        AddIncident(new IncidentDef("disaster", "🌪️ 自然灾害", "不稳定的自然现象发生了，小心火灾或地震！", "Weather",
            weight: 0.5f, cooldownDays: 120, minDayToTrigger: 11, durationDays: 1f));

        // === Random Good ===
        AddIncident(new IncidentDef("wanderer_joins", "🎉 流浪者加入", "一名远方的流浪者加入了你们的殖民地。", "RandomGood",
            weight: 2f, cooldownDays: 10, minDayToTrigger: 2));
        AddIncident(new IncidentDef("trader_visit", "🏪 商人来访", "几名旅行商人经过这里，或许可以与他们交易点物资。", "RandomGood",
            weight: 2.5f, cooldownDays: 5, minDayToTrigger: 3));
        AddIncident(new IncidentDef("cargo_pod", "📦 货物舱坠落", "一个补给舱坠毁在附近，掉落了一些有用的物资！", "RandomGood",
            weight: 1f, cooldownDays: 15, minDayToTrigger: 5));
        AddIncident(new IncidentDef("animal_herd", "🐑 兽群迁徙", "一群动物正在迁徙路过这片区域。", "RandomGood",
            weight: 1.5f, cooldownDays: 10, minDayToTrigger: 3));
        AddIncident(new IncidentDef("aurora", "🌌 极光", "美丽的极光照亮了夜空，为大家带来了希望。", "RandomGood",
            weight: 1f, cooldownDays: 20, minDayToTrigger: 2, durationDays: 0.5f));
        AddIncident(new IncidentDef("party", "🎂 派对", "殖民者们举办了派对，大家心情振奋！", "RandomGood",
            weight: 1.5f, cooldownDays: 8, minDayToTrigger: 3));


        // === Workers ===
        _workers["raid"] = new RaidIncident();
        _workers["animal_attack"] = new AnimalAttackIncident();
        _workers["cold_snap"] = new WeatherIncident();
        _workers["heat_wave"] = new WeatherIncident();
        _workers["disaster"] = new NaturalDisasterIncident();
        _workers["wanderer_joins"] = new RandomGoodIncidents();
        _workers["trader_visit"] = new RandomGoodIncidents();
        _workers["cargo_pod"] = new RandomGoodIncidents();
        _workers["animal_herd"] = new RandomGoodIncidents();
        _workers["aurora"] = new RandomGoodIncidents();
        _workers["party"] = new RandomGoodIncidents();
    }

    private void AddIncident(IncidentDef def) => _allIncidents.Add(def);

    private void OnDayChanged(int day)
    {
        // Calculate threat points
        UpdateThreatPoints();

        // Roll for incident
        string category = PickCategory(day);
        var candidates = GetCandidates(category, day);
        if (candidates.Count == 0) return;

        // Weighted random pick
        var incident = WeightedPick(candidates);
        if (incident == null) return;

        // Fire it
        _lastFiredDay[incident.Id] = day;
        GD.Print($"[Storyteller] Day {day}: Firing '{incident.DisplayName}' (threat: {ThreatPoints:F0})");
        EventBus.FireIncidentTriggered(incident.Id, incident.DisplayName, incident.Description);


        if (_workers.TryGetValue(incident.Id, out var worker))
        {
            worker.Execute(incident, ThreatPoints);
        }

        UpdateThreatLevel();
    }

    public void ForceTriggerIncident(string id)
    {
        var incident = _allIncidents.FirstOrDefault(x => x.Id == id);
        if (incident == null) return;

        UpdateThreatPoints();
        _lastFiredDay[incident.Id] = CurrentDay;

        GD.Print($"[Storyteller] Forced Firing: '{incident.DisplayName}' (threat: {ThreatPoints:F0})");
        EventBus.FireIncidentTriggered(incident.Id, incident.DisplayName, incident.Description);

        if (_workers.TryGetValue(incident.Id, out var worker))
        {
            // Give a fixed minimum threat points for forced events to be visible
            float threat = MathF.Max(ThreatPoints, 200f);
            worker.Execute(incident, threat);
        }

        UpdateThreatLevel();
    }

    public void RefreshThreatLevel()
    {
        UpdateThreatLevel();
    }

    private void UpdateThreatPoints()
    {
        int colonists = PawnManager.Instance?.GetColonistCount() ?? 0;
        int day = CurrentDay;

        // Base: colonists × 100 + day × 20
        ThreatPoints = colonists * 100f + day * 20f;

        // TODO: add colony wealth calculation when inventory tracking is complete
    }

    private string PickCategory(int day)
    {
        // Protection period: only positive events
        if (day <= 3)
            return "RandomGood";

        float roll = (float)_rng.NextDouble();

        // Day 4-10: lighter threats
        if (day <= 10)
        {
            return roll switch
            {
                < 0.30f => "MajorThreat",
                < 0.45f => "Weather",
                _ => "RandomGood",
            };
        }

        // Day 11+: full spectrum
        return roll switch
        {
            < 0.45f => "MajorThreat",
            < 0.65f => "Weather",
            _ => "RandomGood",
        };
    }

    private List<IncidentDef> GetCandidates(string category, int day)
    {
        var result = new List<IncidentDef>();
        foreach (var inc in _allIncidents)
        {
            if (inc.Category != category) continue;
            if (day < inc.MinDayToTrigger) continue;
            if (ThreatPoints < inc.MinThreatPoints) continue;

            // Check cooldown
            if (_lastFiredDay.TryGetValue(inc.Id, out int lastDay))
            {
                if (day - lastDay < inc.CooldownDays) continue;
            }

            result.Add(inc);
        }
        return result;
    }

    private IncidentDef WeightedPick(List<IncidentDef> candidates)
    {
        float totalWeight = candidates.Sum(c => c.Weight);
        if (totalWeight <= 0) return null;

        float roll = (float)_rng.NextDouble() * totalWeight;
        float acc = 0f;
        foreach (var c in candidates)
        {
            acc += c.Weight;
            if (roll <= acc) return c;
        }
        return candidates.Last();
    }

    private void UpdateThreatLevel()
    {
        if (PawnManager.Instance == null) return;

        var hostiles = PawnManager.Instance
            .GetAllEnemies()
            .Where(enemy => enemy.IsAlive)
            .ToList();

        string newLevel;
        if (hostiles.Count == 0)
        {
            newLevel = "peace";
        }
        else if (hostiles.All(enemy => enemy.CurrentAssaultPhase == EnemyAssaultPhase.Preparing))
        {
            newLevel = "alert";
        }
        else
        {
            newLevel = "combat";
        }

        if (newLevel != _currentThreatLevel)
        {
            _currentThreatLevel = newLevel;
            EventBus.FireThreatLevelChanged(newLevel);
            if (newLevel == "combat")
                EventBus.FireCombatStarted();
            else
                EventBus.FireCombatEnded();
        }
    }

    // Called periodically to re-check threat level (e.g., after enemies die)
    public override void _Process(double delta)
    {
        // Check threat level every 2 seconds
        if (Engine.GetProcessFrames() % 120 == 0)
            UpdateThreatLevel();
    }
}

/// <summary>
/// Base class for incident execution logic.
/// </summary>
public abstract class IncidentWorker
{
    protected HostileWaveContext CreateWaveContext(IncidentDef def, EnemyAssaultMode assaultMode, Vector3 rallyCenter)
    {
        long currentTick = TimeManager.Instance?.CurrentTick ?? 0;
        long prepareUntilTick = assaultMode == EnemyAssaultMode.DelayedAttack
            ? currentTick + Settings.HostilePrepareDurationTicks
            : currentTick;
        return new HostileWaveContext(def.Id, assaultMode, prepareUntilTick, rallyCenter);
    }

    public abstract void Execute(IncidentDef def, float threatPoints);
}
