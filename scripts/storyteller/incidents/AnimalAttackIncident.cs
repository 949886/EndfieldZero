using System;
using EndfieldZero.Core;
using EndfieldZero.Managers;
using Godot;

namespace EndfieldZero.Storyteller.Incidents;

/// <summary>
/// Animal attack — spawns 1-4 fast hostile animals.
/// Lower HP but higher speed. Faction = "Animal".
/// </summary>
public class AnimalAttackIncident : IncidentWorker
{
    private static readonly Random Rng = new();

    private static readonly string[] AnimalNames =
    {
        "野狼", "疯熊", "狂犬", "野猪", "猛虎",
    };

    public override void Execute(IncidentDef def, float threatPoints)
    {
        if (PawnManager.Instance == null) return;

        int count = Mathf.Clamp((int)(threatPoints / Settings.AnimalAttackCountThreatDivisor), 1, Settings.AnimalAttackMaxCount);

        var center = PawnManager.Instance.GetColonyCenter();
        float edgeDist = 50f * Settings.BlockPixelSize;

        // Random direction
        float angle = (float)(Rng.NextDouble() * Math.PI * 2.0);
        Vector3 spawnBase = center + new Vector3(
            Mathf.Cos(angle) * edgeDist, 0f, Mathf.Sin(angle) * edgeDist);

        GD.Print($"[AnimalAttack] Spawning {count} animals");

        for (int i = 0; i < count; i++)
        {
            string name = AnimalNames[Rng.Next(AnimalNames.Length)];
            float spacing = 2f * Settings.BlockPixelSize;
            Vector3 pos = spawnBase + new Vector3(i * spacing, 0f, 0f);

            var animal = PawnManager.Instance.SpawnHostile(pos, "Animal", "", name);

            // Animals: high agility, moderate strength
            animal.Data.Agility = Rng.Next(Settings.AnimalAttackAgilityMin, Settings.AnimalAttackAgilityMax + 1);
            animal.Data.Strength = Rng.Next(Settings.AnimalAttackStrengthMin, Settings.AnimalAttackStrengthMax + 1);
        }
    }
}
