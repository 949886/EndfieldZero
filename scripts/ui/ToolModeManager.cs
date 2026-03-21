using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.Pathfinding;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Manages tool modes and job designation.
/// Handles creating/cancelling jobs when the player drags across the map
/// in Mine/Construct/Grow/Cancel modes.
///
/// Hotkeys:
///   Q — Select mode (default)
///   M — Mine mode
///   B — Construct mode
///   G — Grow mode
///   X — Cancel mode
///   Escape — Back to Select mode
/// </summary>
public partial class ToolModeManager : Control
{
    /// <summary>Current active tool mode.</summary>
    public ToolMode CurrentMode { get; private set; } = ToolMode.Select;

    /// <summary>Singleton.</summary>
    public static ToolModeManager Instance { get; private set; }

    // Drag designation state
    private bool _isDragging;
    private Vector2I _dragStart;
    private Vector2I _dragEnd;
    private Vector2 _dragScreenStart;
    private Vector2 _dragScreenEnd;

    // Track designated blocks to avoid duplicate jobs
    private readonly HashSet<Vector2I> _designatedBlocks = new();

    // Visual colors per mode
    private static readonly Dictionary<ToolMode, Color> ModeColors = new()
    {
        { ToolMode.Mine, new Color(1f, 0.3f, 0.2f, 0.3f) },       // Red
        { ToolMode.Construct, new Color(0.3f, 0.5f, 1f, 0.3f) },   // Blue
        { ToolMode.Grow, new Color(0.3f, 0.9f, 0.3f, 0.3f) },      // Green
        { ToolMode.Cancel, new Color(1f, 0.8f, 0.2f, 0.3f) },      // Yellow
    };

    private static readonly Dictionary<ToolMode, Color> ModeBorderColors = new()
    {
        { ToolMode.Mine, new Color(1f, 0.2f, 0.1f, 0.8f) },
        { ToolMode.Construct, new Color(0.2f, 0.4f, 1f, 0.8f) },
        { ToolMode.Grow, new Color(0.2f, 0.8f, 0.2f, 0.8f) },
        { ToolMode.Cancel, new Color(1f, 0.7f, 0.1f, 0.8f) },
    };

    public override void _Ready()
    {
        Instance = this;
        MouseFilter = MouseFilterEnum.Ignore; // Don't consume events when in Select mode
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            var oldMode = CurrentMode;
            switch (key.Keycode)
            {
                case Key.Q: CurrentMode = ToolMode.Select; break;
                case Key.M: CurrentMode = ToolMode.Mine; break;
                case Key.B: CurrentMode = ToolMode.Construct; break;
                case Key.G: CurrentMode = ToolMode.Grow; break;
                case Key.X: CurrentMode = ToolMode.Cancel; break;
                case Key.Escape when CurrentMode != ToolMode.Select:
                    CurrentMode = ToolMode.Select;
                    break;
                default: return;
            }

            if (oldMode != CurrentMode)
            {
                // Update mouse filter: Pass when Tools active, Ignore when Select
                MouseFilter = CurrentMode == ToolMode.Select
                    ? MouseFilterEnum.Ignore
                    : MouseFilterEnum.Pass;
                _isDragging = false;
                QueueRedraw();
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (CurrentMode == ToolMode.Select) return;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    StartDrag(mb.Position);
                    GetViewport().SetInputAsHandled();
                }
                else if (_isDragging)
                {
                    EndDrag(mb.Position);
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                // Right-click exits tool mode
                CurrentMode = ToolMode.Select;
                MouseFilter = MouseFilterEnum.Ignore;
                _isDragging = false;
                QueueRedraw();
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            _dragScreenEnd = mm.Position;
            UpdateDragBlock(mm.Position);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!_isDragging || CurrentMode == ToolMode.Select) return;

        // Draw designation rectangle on screen
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

        // Apply designation to all blocks in the rectangle
        ApplyDesignation();
    }

    private void ApplyDesignation()
    {
        int minX = Mathf.Min(_dragStart.X, _dragEnd.X);
        int maxX = Mathf.Max(_dragStart.X, _dragEnd.X);
        int minZ = Mathf.Min(_dragStart.Y, _dragEnd.Y);
        int maxZ = Mathf.Max(_dragStart.Y, _dragEnd.Y);

        int count = 0;

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
                    case ToolMode.Construct:
                        if (CreateConstructDesignation(coord)) count++;
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

        if (count > 0)
            GD.Print($"[ToolMode] {CurrentMode}: {count} blocks designated");
    }

    // --- Designation creators ---

    private bool CreateMineDesignation(Vector2I blockCoord)
    {
        if (_designatedBlocks.Contains(blockCoord)) return false;

        // Only mine solid blocks
        if (WorldManager.Instance == null) return false;
        var block = WorldManager.Instance.GetBlock(blockCoord.X, blockCoord.Y);
        if (block.IsAir) return false;

        var def = BlockRegistry.Instance.GetDef(block.TypeId);
        if (def == null || !def.IsSolid) return false;

        // Create job
        JobSystem.Instance?.CreateMineJob(blockCoord.X, blockCoord.Y);
        _designatedBlocks.Add(blockCoord);
        return true;
    }

    private bool CreateConstructDesignation(Vector2I blockCoord)
    {
        if (_designatedBlocks.Contains(blockCoord)) return false;

        // Only construct on empty spaces
        if (WorldManager.Instance == null) return false;
        var block = WorldManager.Instance.GetBlock(blockCoord.X, blockCoord.Y);
        if (!block.IsAir) return false;

        JobSystem.Instance?.CreateConstructJob(blockCoord.X, blockCoord.Y);
        _designatedBlocks.Add(blockCoord);
        return true;
    }

    private bool CreateGrowDesignation(Vector2I blockCoord)
    {
        if (_designatedBlocks.Contains(blockCoord)) return false;

        // Only grow on empty spaces
        if (WorldManager.Instance == null) return false;
        var block = WorldManager.Instance.GetBlock(blockCoord.X, blockCoord.Y);
        if (!block.IsAir) return false;

        JobSystem.Instance?.CreateGrowJob(blockCoord.X, blockCoord.Y);
        _designatedBlocks.Add(blockCoord);
        return true;
    }

    private bool CancelDesignation(Vector2I blockCoord)
    {
        if (!_designatedBlocks.Contains(blockCoord)) return false;

        // Find and cancel the job at this coordinate
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
        return true;
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
        if (camera == null) return Vector2I.Zero;

        Vector3 worldPos = SelectionManager.ScreenToWorldXZ(screenPos, camera);
        return PathfindingService.WorldToBlock(worldPos);
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
