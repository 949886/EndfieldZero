using System;
using Cherry.Core;
using Cherry.Managers;
using Cherry.Pawn;
using Godot;

namespace Cherry.Storyteller.Incidents;

/// <summary>
/// Raid incident — spawns armed hostiles at map edge.
/// Enemy count scales with threat points.
/// </summary>
public class RaidIncident : IncidentWorker
{
    private static readonly Random Rng = new();

    private static readonly string[] RaiderNames =
    {
        "匪徒", "强盗", "劫掠者", "流寇", "海盗",
        "暴徒", "佣兵", "狂战士", "掠夺者", "入侵者",
    };

    public override void Execute(IncidentDef def, float threatPoints)
    {
        if (PawnManager.Instance == null) return;

        int count = Mathf.Clamp((int)(threatPoints / Settings.RaidCountThreatDivisor), 1, Settings.RaidMaxCount);

        // Determine weapons based on threat
        string[] weapons;
        if (threatPoints < Settings.RaidLowThreatWeaponThreshold)
            weapons = new[] { "", "knife" };           // fist and knife
        else if (threatPoints < Settings.RaidMidThreatWeaponThreshold)
            weapons = new[] { "knife", "spear", "bow" };
        else
            weapons = new[] { "spear", "hammer", "bow", "crossbow" };

        // Pick a spawn edge
        Vector3 spawnCenter = GetEdgeSpawn();
        float spacing = 2f * Settings.BlockPixelSize;
        EnemyAssaultMode assaultMode = Rng.NextDouble() < Settings.RaidImmediateAttackChance
            ? EnemyAssaultMode.ImmediateAttack
            : EnemyAssaultMode.DelayedAttack;
        var waveContext = CreateWaveContext(def, assaultMode, spawnCenter);

        GD.Print($"[Raid] Spawning {count} raiders (threat: {threatPoints:F0})");

        for (int i = 0; i < count; i++)
        {
            string weapon = weapons[Rng.Next(weapons.Length)];
            string name = RaiderNames[Rng.Next(RaiderNames.Length)];

            Vector3 offset = new Vector3((i - count / 2f) * spacing, 0f, Rng.Next(-2, 2) * spacing * 0.5f);
            Vector3 pos = spawnCenter + offset;

            var raider = PawnManager.Instance.SpawnHostile(pos, "Hostile", weapon, name, waveContext);

            // Scale stats slightly with threat
            float statBonus = Mathf.Min(threatPoints / Settings.RaidStatBonusThreatDivisor, Settings.RaidStatBonusCap);
            raider.Data.Strength += statBonus;
            raider.Data.Agility += statBonus * 0.5f;
        }
    }

    private Vector3 GetEdgeSpawn()
    {
        var center = PawnManager.Instance.GetColonyCenter();
        float mapEdge = 60f * Settings.BlockPixelSize;

        // Random direction
        int dir = Rng.Next(4);
        return dir switch
        {
            0 => center + new Vector3(mapEdge, 0, 0),   // East
            1 => center + new Vector3(-mapEdge, 0, 0),   // West
            2 => center + new Vector3(0, 0, mapEdge),    // South
            _ => center + new Vector3(0, 0, -mapEdge),   // North
        };
    }
}
