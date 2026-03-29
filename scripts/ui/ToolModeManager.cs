using System.Collections.Generic;
using EndfieldZero.Building;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.Pathfinding;
using EndfieldZero.World;
using EndfieldZero.Zone;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Manages tool modes and job designation.
/// Handles creating/cancelling jobs and blueprints when the player interacts.
///
/// Hotkeys:
///   Q — Select mode (default)
///   M — Mine mode
///   B — Construct mode (shows BuildSubMenu, click to place blueprints)
///   G — Grow mode
///   Z — Zone mode (sub-menu: Stockpile/Growing/Home/Dumping)
///   X — Cancel mode
///   R — Rotate blueprint (in Construct mode)
///   Escape — Back to Select mode
/// </summary>
public partial class ToolModeManager : Control
{
    public enum ConstructPlacementMode
    {
        Brush,
        Box,
    }

    /// <summary>Current active tool mode.</summary>
    public ToolMode CurrentMode { get; private set; } = ToolMode.Select;

    /// <summary>Singleton.</summary>
    public static ToolModeManager Instance { get; private set; }

    /// <summary>Selected building for Construct mode.</summary>
    public BuildingDef SelectedBuildingDef { get; set; }

    /// <summary>Current blueprint rotation (0-3).</summary>
    public int BuildRotation { get; set; }

    /// <summary>How Construct mode places blueprints.</summary>
    public ConstructPlacementMode PlacementMode { get; set; } = ConstructPlacementMode.Brush;

    /// <summary>Selected zone type for Zone mode.</summary>
    public string SelectedZoneType { get; set; } = "Stockpile";

    // Drag designation state
    private bool _isDragging;
    private Vector2I _dragStart;
    private Vector2I _dragEnd;
    private Vector2 _dragScreenStart;
    private Vector2 _dragScreenEnd;
    private bool _constructPaintActive;
    private Vector2I _lastConstructPaintCell = new(int.MinValue, int.MinValue);

    // Track designated blocks to avoid duplicate jobs
    private readonly HashSet<Vector2I> _designatedBlocks = new();

    // Visual colors per mode
    private static readonly Dictionary<ToolMode, Color> ModeColors = new()
    {
        { ToolMode.Mine, new Color(1f, 0.3f, 0.2f, 0.3f) },
        { ToolMode.Construct, new Color(0.3f, 0.5f, 1f, 0.3f) },
        { ToolMode.Grow, new Color(0.3f, 0.9f, 0.3f, 0.3f) },
        { ToolMode.Zone, new Color(0.8f, 0.6f, 0.2f, 0.3f) },
        { ToolMode.Cancel, new Color(1f, 0.8f, 0.2f, 0.3f) },
    };

    private static readonly Dictionary<ToolMode, Color> ModeBorderColors = new()
    {
        { ToolMode.Mine, new Color(1f, 0.2f, 0.1f, 0.8f) },
        { ToolMode.Construct, new Color(0.2f, 0.4f, 1f, 0.8f) },
        { ToolMode.Grow, new Color(0.2f, 0.8f, 0.2f, 0.8f) },
        { ToolMode.Zone, new Color(0.8f, 0.5f, 0.1f, 0.8f) },
        { ToolMode.Cancel, new Color(1f, 0.7f, 0.1f, 0.8f) },
    };

    public override void _Ready()
    {
        Instance = this;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.AltPressed && (key.Keycode == Key.Q || key.Keycode == Key.E))
                return;

            var oldMode = CurrentMode;
            switch (key.Keycode)
            {
                case Key.Q: CurrentMode = ToolMode.Select; break;
                case Key.M: CurrentMode = ToolMode.Mine; break;
                case Key.B: CurrentMode = ToolMode.Construct; break;
                case Key.G: CurrentMode = ToolMode.Grow; break;
                case Key.Z: CurrentMode = ToolMode.Zone; break;
                case Key.X: CurrentMode = ToolMode.Cancel; break;
                case Key.R when CurrentMode == ToolMode.Construct:
                    BuildRotation = (BuildRotation + 1) % 4;
                    return;
                case Key.Escape when CurrentMode != ToolMode.Select:
                    CurrentMode = ToolMode.Select;
                    break;
                default: return;
            }

            if (oldMode != CurrentMode)
            {
                MouseFilter = CurrentMode == ToolMode.Select
                    ? MouseFilterEnum.Ignore
                    : MouseFilterEnum.Pass;
                _isDragging = false;
                _constructPaintActive = false;
                _lastConstructPaintCell = new Vector2I(int.MinValue, int.MinValue);
                QueueRedraw();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (CurrentMode == ToolMode.Select) return;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    if (CurrentMode == ToolMode.Construct && SelectedBuildingDef != null)
                    {
                        if (PlacementMode == ConstructPlacementMode.Brush)
                        {
                            _constructPaintActive = true;
                            _lastConstructPaintCell = new Vector2I(int.MinValue, int.MinValue);
                            PaintBlueprintAtScreen(mb.Position);
                        }
                        else
                        {
                            StartDrag(mb.Position);
                        }
                    }
                    else
                    {
                        StartDrag(mb.Position);
                    }
                    GetViewport().SetInputAsHandled();
                }
                else if (_isDragging)
                {
                    EndDrag(mb.Position);
                    GetViewport().SetInputAsHandled();
                }
                else if (!mb.Pressed && CurrentMode == ToolMode.Construct)
                {
                    _constructPaintActive = false;
                    _lastConstructPaintCell = new Vector2I(int.MinValue, int.MinValue);
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                CurrentMode = ToolMode.Select;
                MouseFilter = MouseFilterEnum.Ignore;
                _isDragging = false;
                _constructPaintActive = false;
                _lastConstructPaintCell = new Vector2I(int.MinValue, int.MinValue);
                SelectedBuildingDef = null;
                QueueRedraw();
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (CurrentMode == ToolMode.Construct &&
                PlacementMode == ConstructPlacementMode.Brush &&
                _constructPaintActive &&
                SelectedBuildingDef != null)
            {
                PaintBlueprintAtScreen(mm.Position);
                GetViewport().SetInputAsHandled();
            }

            if (_isDragging)
            {
                _dragScreenEnd = mm.Position;
                UpdateDragBlock(mm.Position);
                QueueRedraw();
            }

            // Update blueprint preview
            if (CurrentMode == ToolMode.Construct && SelectedBuildingDef != null)
            {
                UpdateBlueprintPreview(mm.Position);
            }
        }
    }

    public void SetMode(ToolMode mode)
    {
        if (CurrentMode == mode)
            return;

        CurrentMode = mode;
        MouseFilter = CurrentMode == ToolMode.Select
            ? MouseFilterEnum.Ignore
            : MouseFilterEnum.Pass;
        _isDragging = false;
        _constructPaintActive = false;
        _lastConstructPaintCell = new Vector2I(int.MinValue, int.MinValue);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_isDragging || CurrentMode == ToolMode.Select) return;

        if (ModeColors.TryGetValue(CurrentMode, out var fill) &&
            ModeBorderColors.TryGetValue(CurrentMode, out var border))
        {
            var rect = GetScreenRect(_dragScreenStart, _dragScreenEnd);
            DrawRect(rect, fill, filled: true);
            DrawRect(rect, border, filled: false, width: 2f);
        }
    }

    // --- Drag logic ---

    private void StartDrag(Vector2 screenPos)
    {
        _isDragging = true;
        _dragScreenStart = screenPos;
        _dragScreenEnd = screenPos;
        _dragStart = ScreenToBlock(screenPos);
        _dragEnd = _dragStart;
    }

    private void UpdateDragBlock(Vector2 screenPos)
    {
        _dragEnd = ScreenToBlock(screenPos);
    }

    private void EndDrag(Vector2 screenPos)
    {
        _isDragging = false;
        _dragEnd = ScreenToBlock(screenPos);
        QueueRedraw();

        if (CurrentMode == ToolMode.Construct &&
            PlacementMode == ConstructPlacementMode.Box &&
            SelectedBuildingDef != null)
        {
            PlaceBlueprintsInDraggedArea();
            return;
        }

        ApplyDesignation();
    }

    private void ApplyDesignation()
    {
        int minX = Mathf.Min(_dragStart.X, _dragEnd.X);
        int maxX = Mathf.Max(_dragStart.X, _dragEnd.X);
        int minZ = Mathf.Min(_dragStart.Y, _dragEnd.Y);
        int maxZ = Mathf.Max(_dragStart.Y, _dragEnd.Y);

        int count = 0;

        if (CurrentMode == ToolMode.Zone)
        {
            // Collect cells and create zone
            var cells = new List<Vector2I>();
            for (int bz = minZ; bz <= maxZ; bz++)
                for (int bx = minX; bx <= maxX; bx++)
                    cells.Add(new Vector2I(bx, bz));

            var zone = ZoneSystem.Instance?.CreateZone(SelectedZoneType, cells);
            if (zone != null) count = zone.Cells.Count;
        }
        else
        {
            for (int bz = minZ; bz <= maxZ; bz++)
            {
                for (int bx = minX; bx <= maxX; bx++)
                {
                    var coord = new Vector2I(bx, bz);

                    switch (CurrentMode)
                    {
                        case ToolMode.Mine:
                            if (CreateMineDesignation(coord)) count++;
                            break;
                        case ToolMode.Grow:
                            if (CreateGrowDesignation(coord)) count++;
                            break;
                        case ToolMode.Cancel:
                            if (CancelDesignation(coord)) count++;
                            break;
                    }
                }
            }
        }

        if (count > 0)
            GD.Print($"[ToolMode] {CurrentMode}: {count} blocks designated");
    }

    // --- Blueprint placement ---

    private void PlaceBlueprintAtMouse(Vector2 screenPos)
    {
        if (SelectedBuildingDef == null || BlueprintSystem.Instance == null) return;

        var blockCoord = ScreenToBlock(screenPos);
        var bp = BlueprintSystem.Instance.PlaceBlueprint(SelectedBuildingDef, blockCoord, BuildRotation);
        if (bp != null)
        {
            // Track cells for cancel mode
            foreach (var cell in bp.OccupiedCells())
                _designatedBlocks.Add(cell);
        }
    }

    private void PaintBlueprintAtScreen(Vector2 screenPos)
    {
        if (SelectedBuildingDef == null || BlueprintSystem.Instance == null) return;

        var blockCoord = ScreenToBlock(screenPos);
        if (blockCoord == _lastConstructPaintCell) return;

        _lastConstructPaintCell = blockCoord;
        PlaceBlueprintAtMouse(screenPos);
    }

    private void PlaceBlueprintsInDraggedArea()
    {
        if (SelectedBuildingDef == null || BlueprintSystem.Instance == null) return;

        var step = GetBuildingFootprint();
        int minX = Mathf.Min(_dragStart.X, _dragEnd.X);
        int maxX = Mathf.Max(_dragStart.X, _dragEnd.X);
        int minZ = Mathf.Min(_dragStart.Y, _dragEnd.Y);
        int maxZ = Mathf.Max(_dragStart.Y, _dragEnd.Y);

        for (int bz = minZ; bz <= maxZ; bz += step.Y)
        {
            for (int bx = minX; bx <= maxX; bx += step.X)
            {
                PlaceBlueprintAtBlock(new Vector2I(bx, bz));
            }
        }
    }

    private Vector2I GetBuildingFootprint()
    {
        if (SelectedBuildingDef == null)
            return Vector2I.One;

        return (BuildRotation % 2) == 1
            ? new Vector2I(SelectedBuildingDef.Size.Y, SelectedBuildingDef.Size.X)
            : SelectedBuildingDef.Size;
    }

    private void PlaceBlueprintAtBlock(Vector2I blockCoord)
    {
        if (SelectedBuildingDef == null || BlueprintSystem.Instance == null) return;

        var bp = BlueprintSystem.Instance.PlaceBlueprint(SelectedBuildingDef, blockCoord, BuildRotation);
        if (bp == null) return;

        foreach (var cell in bp.OccupiedCells())
            _designatedBlocks.Add(cell);
    }

    private void UpdateBlueprintPreview(Vector2 screenPos)
    {
        var overlay = GetTree().Root.GetChild(0)?.GetNodeOrNull<BlueprintOverlay>("BlueprintOverlay");
        if (overlay != null)
        {
            overlay.ShowPreview = true;
            overlay.PreviewDef = SelectedBuildingDef;
            overlay.PreviewCoord = ScreenToBlock(screenPos);
            overlay.PreviewRotation = BuildRotation;
        }
    }

    // --- Designation creators ---

    private bool CreateMineDesignation(Vector2I blockCoord)
    {
        if (_designatedBlocks.Contains(blockCoord)) return false;
        if (WorldManager.Instance == null) return false;
        var block = WorldManager.Instance.GetBlock(blockCoord.X, blockCoord.Y);
        if (block.IsAir) return false;

        var def = BlockRegistry.Instance.GetDef(block.TypeId);
        if (def == null || !def.IsSolid) return false;

        JobSystem.Instance?.CreateMineJob(blockCoord.X, blockCoord.Y);
        _designatedBlocks.Add(blockCoord);
        return true;
    }

    private bool CreateGrowDesignation(Vector2I blockCoord)
    {
        if (_designatedBlocks.Contains(blockCoord)) return false;
        if (WorldManager.Instance == null) return false;
        var block = WorldManager.Instance.GetBlock(blockCoord.X, blockCoord.Y);

        // Grow on walkable, non-solid terrain (grass, dirt, etc.)
        var blockDef = BlockRegistry.Instance.GetDef(block.TypeId);
        if (blockDef == null || blockDef.IsSolid) return false;
        if (blockDef.MoveSpeedMod <= 0f) return false;

        JobSystem.Instance?.CreateGrowJob(blockCoord.X, blockCoord.Y);
        _designatedBlocks.Add(blockCoord);
        return true;
    }

    private bool CancelDesignation(Vector2I blockCoord)
    {
        bool cancelled = false;

        // Cancel jobs at this coordinate
        if (_designatedBlocks.Contains(blockCoord))
        {
            if (JobSystem.Instance != null)
            {
                foreach (var job in JobSystem.Instance.AllJobs)
                {
                    if (job.TargetBlockCoord == blockCoord &&
                        job.Status != JobStatus.Completed)
                    {
                        job.Cancel();
                        JobSystem.Instance.RemoveJob(job.Id);
                        break;
                    }
                }
            }
            _designatedBlocks.Remove(blockCoord);
            cancelled = true;
        }

        // Cancel blueprints at this coordinate
        BlueprintSystem.Instance?.CancelBlueprintAt(blockCoord);

        // Cancel zones at this coordinate
        ZoneSystem.Instance?.DeleteZoneAt(blockCoord);

        return cancelled;
    }

    /// <summary>Called when a job completes — remove from designated set.</summary>
    public void OnJobCompleted(Vector2I blockCoord)
    {
        _designatedBlocks.Remove(blockCoord);
    }

    /// <summary>Check if a block is designated.</summary>
    public bool IsDesignated(Vector2I blockCoord) => _designatedBlocks.Contains(blockCoord);

    // --- Coordinate utilities ---

    private Vector2I ScreenToBlock(Vector2 screenPos)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null || WorldManager.Instance == null) return Vector2I.Zero;
        var hit = WorldManager.Instance.ScreenToBlockHit(screenPos, camera);
        return hit.Hit ? hit.BlockCoord : Vector2I.Zero;
    }

    private static Rect2 GetScreenRect(Vector2 a, Vector2 b)
    {
        float x = Mathf.Min(a.X, b.X);
        float y = Mathf.Min(a.Y, b.Y);
        float w = Mathf.Abs(b.X - a.X);
        float h = Mathf.Abs(b.Y - a.Y);
        return new Rect2(x, y, Mathf.Max(w, 1), Mathf.Max(h, 1));
    }
}
