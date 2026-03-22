using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.Zone;
using Godot;

namespace EndfieldZero.Items;

/// <summary>
/// Manages all item stacks in the world.
/// Handles spawning, merging, removal, and automatic haul job creation.
///
/// Every few seconds, scans for unhauled ground items and creates
/// Haul jobs to move them to the nearest stockpile with space.
/// </summary>
public partial class ItemManager : Node
{
    private readonly Dictionary<int, ItemStack> _items = new();
    private long _lastHaulScanTick;
    private const int HaulScanInterval = 120; // ~2 seconds

    public static ItemManager Instance { get; private set; }

    public IReadOnlyDictionary<int, ItemStack> AllItems => _items;

    public override void _Ready()
    {
        Instance = this;
        EventBus.Tick += OnTick;
    }

    public override void _ExitTree()
    {
        EventBus.Tick -= OnTick;
    }

    /// <summary>Spawn an item stack at a world block coordinate.</summary>
    public ItemStack SpawnItem(Vector2I blockCoord, string itemDefId, int count)
    {
        var def = ItemRegistry.Instance.GetDef(itemDefId);
        if (def == null)
        {
            GD.PrintErr($"[ItemManager] Unknown item: {itemDefId}");
            return null;
        }

        // Try to merge with existing stack at same location
        var existing = GetItemsAt(blockCoord)
            .FirstOrDefault(s => s.Def.Id == itemDefId && s.State == ItemState.OnGround);

        if (existing != null && existing.Count + count <= def.MaxStack)
        {
            existing.Count += count;
            GD.Print($"[ItemManager] Merged {count}× {def.DisplayName} at {blockCoord} (total: {existing.Count})");
            return existing;
        }

        // Create new stack
        var stack = new ItemStack();
        stack.Init(def, blockCoord, count);

        var container = GetTree().Root.GetChild(0)?.GetNodeOrNull<Node3D>("EntityContainer");
        if (container != null)
            container.AddChild(stack);
        else
            GetTree().Root.GetChild(0).AddChild(stack);

        _items[stack.ItemId] = stack;
        GD.Print($"[ItemManager] Spawned {count}× {def.DisplayName} at {blockCoord}");
        return stack;
    }

    /// <summary>Remove an item stack from the world.</summary>
    public void RemoveItem(int itemId)
    {
        if (_items.TryGetValue(itemId, out var stack))
        {
            stack.QueueFree();
            _items.Remove(itemId);
        }
    }

    /// <summary>Get item stack by ID.</summary>
    public ItemStack GetItem(int itemId) => _items.GetValueOrDefault(itemId);

    /// <summary>Get all items at a block coordinate.</summary>
    public IEnumerable<ItemStack> GetItemsAt(Vector2I coord)
        => _items.Values.Where(s => s.BlockCoord == coord);

    /// <summary>Get all unhauled ground items.</summary>
    public IEnumerable<ItemStack> GetGroundItems()
        => _items.Values.Where(s => s.State == ItemState.OnGround);

    /// <summary>Find the nearest unreserved ground item to a position.</summary>
    public ItemStack FindNearestGroundItem(Vector3 fromPos)
    {
        ItemStack nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var item in _items.Values)
        {
            if (item.State != ItemState.OnGround) continue;

            float dist = fromPos.DistanceTo(item.GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = item;
            }
        }

        return nearest;
    }

    private void OnTick(long tick)
    {
        if (tick - _lastHaulScanTick < HaulScanInterval) return;
        _lastHaulScanTick = tick;

        ScanAndCreateHaulJobs();
    }

    /// <summary>
    /// Scan for ground items and create haul jobs to move them to stockpiles.
    /// </summary>
    private void ScanAndCreateHaulJobs()
    {
        if (JobSystem.Instance == null || ZoneSystem.Instance == null) return;

        foreach (var item in _items.Values.ToList())
        {
            if (item.State != ItemState.OnGround) continue;

            // Check if a haul job already exists for this item
            if (HasExistingHaulJob(item.ItemId)) continue;

            // Find a stockpile slot
            var destCoord = FindStockpileSlot(item);
            if (destCoord == null) continue;

            // Create haul job
            CreateHaulJob(item, destCoord.Value);
        }
    }

    private bool HasExistingHaulJob(int itemId)
    {
        if (JobSystem.Instance == null) return false;
        return JobSystem.Instance.AllJobs.Any(j =>
            j.JobType == "Haul" && j.HaulItemId == itemId &&
            j.Status != JobStatus.Completed && j.Status != JobStatus.Failed);
    }

    /// <summary>Find a stockpile cell with space for this item.</summary>
    private Vector2I? FindStockpileSlot(ItemStack item)
    {
        if (ZoneSystem.Instance == null) return null;

        Vector2I? best = null;
        float bestDist = float.MaxValue;

        foreach (var zone in ZoneSystem.Instance.AllZones)
        {
            if (zone.ZoneType != "Stockpile") continue;

            foreach (var cell in zone.Cells)
            {
                // Check if cell has space
                var existingItems = GetItemsAt(cell);
                var sameType = existingItems.FirstOrDefault(s => s.Def.Id == item.Def.Id);

                if (sameType != null)
                {
                    // Can merge?
                    if (sameType.Count + item.Count > item.Def.MaxStack) continue;
                }
                else
                {
                    // Cell must be empty (no other item types)
                    if (existingItems.Any(s => s.State == ItemState.InStockpile)) continue;
                }

                float dist = item.GlobalPosition.DistanceTo(new Vector3(
                    (cell.X + 0.5f) * Settings.BlockPixelSize,
                    0f,
                    (cell.Y + 0.5f) * Settings.BlockPixelSize));

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = cell;
                }
            }
        }

        return best;
    }

    private void CreateHaulJob(ItemStack item, Vector2I destCoord)
    {
        float px = Settings.BlockPixelSize;

        var job = new Job("Haul", $"搬运{item.Def.DisplayName}")
        {
            TargetBlockCoord = item.BlockCoord,
            TargetWorldPos = item.GlobalPosition,
            HaulItemId = item.ItemId,
            HaulDestCoord = destCoord,
            RequiredSkill = "Strength",
            MinSkillLevel = 0f,
            WorkTicks = 60,  // Quick pickup
            BasePriority = 3,
            XpPerTick = 0.2f,
        };

        item.State = ItemState.Reserved;
        JobSystem.Instance.AddJob(job);
    }
}
