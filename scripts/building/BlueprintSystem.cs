using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Building;

/// <summary>
/// Manages all blueprints — placement, validation, completion, and cancellation.
/// Creates construction jobs in JobSystem when blueprints are placed.
/// </summary>
public partial class BlueprintSystem : Node
{
    private readonly List<Blueprint> _blueprints = new();
    private readonly HashSet<Vector2I> _occupiedCells = new();

    public static BlueprintSystem Instance { get; private set; }

    public IReadOnlyList<Blueprint> AllBlueprints => _blueprints;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// Try to place a blueprint at the given block coordinate.
    /// Returns the blueprint if successful, null if invalid placement.
    /// </summary>
    public Blueprint PlaceBlueprint(BuildingDef def, Vector2I blockCoord, int rotation = 0)
    {
        var bp = new Blueprint(def, blockCoord, rotation);

        // Validate placement
        if (!CanPlace(bp))
        {
            GD.Print($"[Blueprint] Cannot place {def.DisplayName} at {blockCoord}");
            return null;
        }

        // Occupy cells
        foreach (var cell in bp.OccupiedCells())
            _occupiedCells.Add(cell);

        _blueprints.Add(bp);

        // Create construction job
        CreateBuildJob(bp);

        GD.Print($"[Blueprint] Placed {def.DisplayName} at {blockCoord} (ID:{bp.Id})");
        return bp;
    }

    /// <summary>Check if a blueprint can be placed at its position.</summary>
    public bool CanPlace(Blueprint bp)
    {
        if (WorldManager.Instance == null) return false;

        foreach (var cell in bp.OccupiedCells())
        {
            // Check overlap with other blueprints
            if (_occupiedCells.Contains(cell))
                return false;

            // Check world block
            var block = WorldManager.Instance.GetBlock(cell.X, cell.Y);
            var blockDef = BlockRegistry.Instance.GetDef(block.TypeId);

            if (bp.Def.PlacedBlockId != 0)
            {
                // This project uses a single block layer for terrain + finished buildings.
                // Buildable structures therefore replace walkable ground instead of requiring air.
                if (blockDef == null) return false;
                if (blockDef.IsSolid) return false;
                if (block.TypeId == BlockRegistry.WaterId || block.TypeId == BlockRegistry.RiverId)
                    return false;
            }
            else
            {
                // Furniture: can be on walkable terrain, but not on water/walls
                if (blockDef != null && blockDef.IsSolid) return false;
                if (block.TypeId == BlockRegistry.WaterId || block.TypeId == BlockRegistry.RiverId)
                    return false;
            }
        }

        return true;
    }

    /// <summary>Check if a single cell can accept a blueprint.</summary>
    public bool CanPlaceAt(BuildingDef def, Vector2I blockCoord, int rotation = 0)
    {
        var bp = new Blueprint(def, blockCoord, rotation);
        return CanPlace(bp);
    }

    /// <summary>Cancel and remove a blueprint.</summary>
    public void CancelBlueprint(int blueprintId)
    {
        var bp = _blueprints.FirstOrDefault(b => b.Id == blueprintId);
        if (bp == null) return;

        // Free cells
        foreach (var cell in bp.OccupiedCells())
            _occupiedCells.Remove(cell);

        // Cancel linked job
        if (bp.LinkedJobId >= 0)
        {
            var job = JobSystem.Instance?.GetJob(bp.LinkedJobId);
            if (job != null && job.Status != JobStatus.Completed)
            {
                job.Cancel();
                JobSystem.Instance?.RemoveJob(job.Id);
            }
        }

        _blueprints.Remove(bp);
        GD.Print($"[Blueprint] Cancelled {bp.Def.DisplayName} (ID:{bp.Id})");
    }

    /// <summary>Cancel blueprint at a cell coordinate.</summary>
    public void CancelBlueprintAt(Vector2I cell)
    {
        var bp = _blueprints.FirstOrDefault(b => b.OccupiedCells().Contains(cell));
        if (bp != null) CancelBlueprint(bp.Id);
    }

    /// <summary>
    /// Called when a construction job completes.
    /// Places the actual building in the world.
    /// </summary>
    public void CompleteBlueprint(int blueprintId)
    {
        var bp = _blueprints.FirstOrDefault(b => b.Id == blueprintId);
        if (bp == null) return;

        bp.Status = BlueprintStatus.Complete;

        // Place blocks in the world
        if (bp.Def.PlacedBlockId != 0 && WorldManager.Instance != null)
        {
            foreach (var cell in bp.OccupiedCells())
            {
                WorldManager.Instance.SetBlock(cell.X, cell.Y, new Block(bp.Def.PlacedBlockId));
            }
        }

        // Free cells
        foreach (var cell in bp.OccupiedCells())
            _occupiedCells.Remove(cell);

        _blueprints.Remove(bp);
        GD.Print($"[Blueprint] Completed {bp.Def.DisplayName} (ID:{bp.Id})");
    }

    /// <summary>Get blueprint by ID.</summary>
    public Blueprint GetBlueprint(int id) => _blueprints.FirstOrDefault(b => b.Id == id);

    /// <summary>Check if a cell is occupied by any blueprint.</summary>
    public bool IsCellOccupied(Vector2I cell) => _occupiedCells.Contains(cell);

    // --- Job creation ---

    private void CreateBuildJob(Blueprint bp)
    {
        if (JobSystem.Instance == null) return;

        float px = Settings.BlockPixelSize;
        var center = bp.WorldCenter;

        var job = new Job("Construct", $"建造{bp.Def.DisplayName}")
        {
            TargetBlockCoord = bp.BlockCoord,
            TargetWorldPos = center,
            RequiredSkill = bp.Def.RequiredSkill,
            MinSkillLevel = bp.Def.MinSkillLevel,
            WorkTicks = bp.Def.WorkTicks,
            BasePriority = 5,
            XpPerTick = bp.Def.XpPerTick,
            BlueprintId = bp.Id,
        };

        JobSystem.Instance.AddJob(job);
        bp.LinkedJobId = job.Id;
    }

    /// <summary>Cleanup completed blueprints.</summary>
    public override void _Process(double delta)
    {
        _blueprints.RemoveAll(b => b.Status == BlueprintStatus.Complete);
    }
}
