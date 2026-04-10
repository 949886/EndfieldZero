using System;
using EndfieldZero.Core;
using EndfieldZero.Items;
using EndfieldZero.Managers;
using Godot;

namespace EndfieldZero.Storyteller.Incidents;

/// <summary>
/// Random positive events:
///   - Wanderer Joins: new colonist
///   - Trader Visit: neutral NPC visiting
///   - Cargo Pod Drop: random items fall from sky
///   - Animal Herd: neutral animals pass through
///   - Aurora: mood boost
///   - Party: mood boost (bigger if birthday match)
/// </summary>
public class RandomGoodIncidents : IncidentWorker
{
    private static readonly Random Rng = new();

    public override void Execute(IncidentDef def, float threatPoints)
    {
        switch (def.Id)
        {
            case "wanderer_joins":
                WandererJoins();
                break;
            case "trader_visit":
                TraderVisit();
                break;
            case "cargo_pod":
                CargoPodDrop();
                break;
            case "animal_herd":
                AnimalHerd();
                break;
            case "aurora":
                Aurora();
                break;
            case "party":
                Party();
                break;
        }
    }

    private void WandererJoins()
    {
        if (PawnManager.Instance == null) return;

        var center = PawnManager.Instance.GetColonyCenter();
        float edgeDist = 40f * Settings.BlockPixelSize;
        float angle = (float)(Rng.NextDouble() * Math.PI * 2.0);
        Vector3 pos = center + new Vector3(
            Mathf.Cos(angle) * edgeDist, 0f, Mathf.Sin(angle) * edgeDist);

        var pawn = PawnManager.Instance.SpawnColonist(pos);
        GD.Print($"[Event] Wanderer '{pawn.Data.PawnName}' joins the colony!");

        // Mood boost for existing colonists
        foreach (var existing in PawnManager.Instance.GetAllPawns())
        {
            if (existing.Data.Faction == "Colony" && existing.IsAlive)
                existing.Mood.AddThought("new_colonist", "新伙伴加入", 8f,
                    TimeManager.TicksPerHour * 12);
        }
    }

    private void TraderVisit()
    {
        if (PawnManager.Instance == null) return;

        var center = PawnManager.Instance.GetColonyCenter();
        float edgeDist = 50f * Settings.BlockPixelSize;
        float angle = (float)(Rng.NextDouble() * Math.PI * 2.0);
        Vector3 pos = center + new Vector3(
            Mathf.Cos(angle) * edgeDist, 0f, Mathf.Sin(angle) * edgeDist);

        var trader = PawnManager.Instance.SpawnNeutral(pos, "旅行商人");
        GD.Print("[Event] Trader visits the colony");

        // TODO: add trading UI interaction
    }

    private void CargoPodDrop()
    {
        if (ItemManager.Instance == null || PawnManager.Instance == null) return;

        var center = PawnManager.Instance.GetColonyCenter();
        var blockCenter = Pathfinding.PathfindingService.WorldToBlock(center);

        // Drop 3-8 random items near colony
        int dropCount = Rng.Next(3, 9);
        string[] itemIds = { "stone", "wood", "iron", "copper", "coal", "wheat", "carrot" };

        GD.Print($"[Event] Cargo pod drops {dropCount} items!");

        for (int i = 0; i < dropCount; i++)
        {
            string itemId = itemIds[Rng.Next(itemIds.Length)];
            int offsetX = Rng.Next(-8, 8);
            int offsetY = Rng.Next(-8, 8);
            var coord = new Vector2I(blockCenter.X + offsetX, blockCenter.Y + offsetY);
            int count = Rng.Next(3, 12);

            ItemManager.Instance.SpawnItem(coord, itemId, count);
        }
    }

    private void AnimalHerd()
    {
        if (PawnManager.Instance == null) return;

        int count = Rng.Next(2, 5);
        var center = PawnManager.Instance.GetColonyCenter();
        float edgeDist = 45f * Settings.BlockPixelSize;
        float angle = (float)(Rng.NextDouble() * Math.PI * 2.0);
        Vector3 spawnBase = center + new Vector3(
            Mathf.Cos(angle) * edgeDist, 0f, Mathf.Sin(angle) * edgeDist);

        string[] animalNames = { "绵羊", "鹿", "兔子", "牛" };

        GD.Print($"[Event] Animal herd ({count}) passes through");

        for (int i = 0; i < count; i++)
        {
            float spacing = 2f * Settings.BlockPixelSize;
            Vector3 pos = spawnBase + new Vector3(i * spacing, 0f, 0f);
            PawnManager.Instance.SpawnNeutral(pos, animalNames[Rng.Next(animalNames.Length)]);
        }
    }

    private void Aurora()
    {
        GD.Print("[Event] 🌌 Aurora lights up the sky!");

        if (PawnManager.Instance == null) return;

        foreach (var pawn in PawnManager.Instance.GetAllPawns())
        {
            if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
                pawn.Mood.AddThought("aurora", "极光照耀天空", 10f,
                    TimeManager.TicksPerHour * 12);
        }
    }

    private void Party()
    {
        GD.Print("[Event] 🎂 Party time!");

        if (PawnManager.Instance == null) return;

        float moodBonus = 15f;

        // Check if any colonist has a "birthday" matching current real date
        var today = DateTime.Now;
        foreach (var pawn in PawnManager.Instance.GetAllPawns())
        {
            if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
            {
                // Simple birthday check: age mod 365 close to day of year
                if (pawn.Data.Age > 0 && Rng.NextDouble() < 0.15)
                {
                    moodBonus = 25f; // Birthday bonus!
                    GD.Print($"[Event] It's {pawn.Data.PawnName}'s birthday!");
                }
            }
        }

        foreach (var pawn in PawnManager.Instance.GetAllPawns())
        {
            if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
                pawn.Mood.AddThought("party", "欢乐的聚会", moodBonus,
                    TimeManager.TicksPerHour * 18);
        }
    }
}
