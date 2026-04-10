using System;
using EndfieldZero.Core;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Storyteller.Incidents;

/// <summary>
/// Natural disaster: fire or earthquake.
/// Fire: ignites an area, destroys buildings/crops.
/// Earthquake: randomly breaks blocks.
/// </summary>
public class NaturalDisasterIncident : IncidentWorker
{
    private static readonly Random Rng = new();

    public override void Execute(IncidentDef def, float threatPoints)
    {
        bool isFire = Rng.NextDouble() < 0.5;

        if (isFire)
            ExecuteFire();
        else
            ExecuteEarthquake();
    }

    private void ExecuteFire()
    {
        GD.Print("[Disaster] 🔥 Fire breaks out!");

        if (Managers.PawnManager.Instance == null || WorldManager.Instance == null) return;

        var center = Managers.PawnManager.Instance.GetColonyCenter();
        var blockCenter = Pathfinding.PathfindingService.WorldToBlock(center);

        // Fire offset from colony center
        int offsetX = Rng.Next(-15, 15);
        int offsetY = Rng.Next(-15, 15);
        int fireX = blockCenter.X + offsetX;
        int fireY = blockCenter.Y + offsetY;

        // Burn 5-12 blocks (trees, crops, wood structures)
        int burnCount = Rng.Next(5, 13);
        int burned = 0;

        for (int dx = -3; dx <= 3 && burned < burnCount; dx++)
        {
            for (int dy = -3; dy <= 3 && burned < burnCount; dy++)
            {
                int bx = fireX + dx;
                int by = fireY + dy;

                var block = WorldManager.Instance.GetBlock(bx, by);
                if (block.TypeId == BlockRegistry.TreeId ||
                    block.TypeId == BlockRegistry.ConiferTreeId ||
                    block.TypeId == BlockRegistry.BirchTreeId ||
                    block.TypeId == BlockRegistry.WoodWallId ||
                    block.TypeId == BlockRegistry.WoodDoorId)
                {
                    WorldManager.Instance.SetBlock(bx, by, Block.Air);
                    burned++;
                }
            }
        }

        // Kill crops in fire area
        Farming.CropManager.Instance?.DamageRandomCrops(0.2f);

        GD.Print($"[Disaster] Fire burned {burned} blocks");

        // Mood impact
        if (Managers.PawnManager.Instance != null)
        {
            foreach (var pawn in Managers.PawnManager.Instance.GetAllPawns())
            {
                if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
                    pawn.Mood.AddThought("fire_disaster", "殖民地遭遇火灾", -12f,
                        TimeManager.TicksPerHour * 12);
            }
        }
    }

    private void ExecuteEarthquake()
    {
        GD.Print("[Disaster] 🌋 Earthquake!");

        if (Managers.PawnManager.Instance == null || WorldManager.Instance == null) return;

        var center = Managers.PawnManager.Instance.GetColonyCenter();
        var blockCenter = Pathfinding.PathfindingService.WorldToBlock(center);

        // Destroy 3-8 random solid blocks near colony
        int destroyCount = Rng.Next(3, 9);
        int destroyed = 0;

        for (int attempt = 0; attempt < 50 && destroyed < destroyCount; attempt++)
        {
            int bx = blockCenter.X + Rng.Next(-20, 20);
            int by = blockCenter.Y + Rng.Next(-20, 20);

            var block = WorldManager.Instance.GetBlock(bx, by);
            if (block.TypeId == BlockRegistry.StoneWallId ||
                block.TypeId == BlockRegistry.WoodWallId ||
                block.TypeId == BlockRegistry.StoneId)
            {
                WorldManager.Instance.SetBlock(bx, by, Block.Air);
                destroyed++;

                // Drop stone debris
                Items.ItemManager.Instance?.SpawnItem(new Vector2I(bx, by), "stone", 1);
            }
        }

        GD.Print($"[Disaster] Earthquake destroyed {destroyed} blocks");

        // Mood
        if (Managers.PawnManager.Instance != null)
        {
            foreach (var pawn in Managers.PawnManager.Instance.GetAllPawns())
            {
                if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
                    pawn.Mood.AddThought("earthquake", "地震摧毁了建筑", -10f,
                        TimeManager.TicksPerHour * 8);
            }
        }

        // Small HP damage to randomly positioned colonists
        foreach (var pawn in Managers.PawnManager.Instance.GetAllPawns())
        {
            if (pawn.Data.Faction == "Colony" && pawn.IsAlive && Rng.NextDouble() < 0.3)
            {
                pawn.Health?.TakeDamage(Rng.Next(3, 10), -1, "躯干");
            }
        }
    }
}
