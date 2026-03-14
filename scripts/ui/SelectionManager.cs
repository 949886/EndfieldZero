using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Managers;
using EndfieldZero.Pathfinding;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// StarCraft-style unit selection and command system.
///
/// Controls:
///   Left-click        — Select single pawn
///   Left-drag         — Box select multiple pawns
///   Right-click       — Move selected pawns to target (with A* pathfinding)
///   Shift + Left-click — Add/remove from selection
///   Ctrl+A            — Select all
///   Escape             — Deselect all
///
/// Renders a green selection box overlay and selection circles under pawns.
/// </summary>
public partial class SelectionManager : Control
{
    private readonly List<Pawn.Pawn> _selected = new();
    private bool _isBoxSelecting;
    private Vector2 _boxStart;
    private Vector2 _boxEnd;

    // Box select visual
    private static readonly Color BoxFillColor = new(0.2f, 0.8f, 0.2f, 0.15f);
    private static readonly Color BoxBorderColor = new(0.3f, 1f, 0.3f, 0.8f);
    private static readonly Color SelectionCircleColor = new(0.3f, 1f, 0.3f, 0.6f);

    /// <summary>Currently selected pawns (read-only).</summary>
    public IReadOnlyList<Pawn.Pawn> Selected => _selected;

    public override void _Ready()
    {
        // Cover entire viewport for input capture
        MouseFilter = MouseFilterEnum.Pass;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            HandleMouseButton(mb);
        }
        else if (@event is InputEventMouseMotion mm && _isBoxSelecting)
        {
            _boxEnd = mm.Position;
            QueueRedraw();
        }
        else if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            HandleKey(key);
        }
    }

    public override void _Draw()
    {
        // Draw box selection rectangle
        if (_isBoxSelecting)
        {
            var rect = GetBoxRect();
            DrawRect(rect, BoxFillColor, filled: true);
            DrawRect(rect, BoxBorderColor, filled: false, width: 2f);
        }
    }

    // --- Input handlers ---

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isBoxSelecting = true;
                _boxStart = mb.Position;
                _boxEnd = mb.Position;
            }
            else if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                QueueRedraw();

                var rect = GetBoxRect();
                bool isClick = rect.Size.Length() < 5f;
                bool additive = Input.IsKeyPressed(Key.Shift);

                if (isClick)
                {
                    HandleClickSelect(mb.Position, additive);
                }
                else
                {
                    HandleBoxSelect(rect, additive);
                }
            }
        }
        else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            HandleRightClick(mb.Position);
        }
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Keycode == Key.Escape)
        {
            DeselectAll();
        }
        else if (key.Keycode == Key.A && key.CtrlPressed)
        {
            SelectAll();
        }
    }

    // --- Selection logic ---

    private void HandleClickSelect(Vector2 screenPos, bool additive)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        Pawn.Pawn closest = FindPawnAtScreenPos(screenPos, camera, 60f);

        if (closest != null)
        {
            if (additive)
            {
                if (_selected.Contains(closest))
                    Deselect(closest);
                else
                    Select(closest);
            }
            else
            {
                DeselectAll();
                Select(closest);
            }
        }
        else if (!additive)
        {
            DeselectAll();
        }
    }

    private void HandleBoxSelect(Rect2 screenRect, bool additive)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null || PawnManager.Instance == null) return;

        if (!additive) DeselectAll();

        foreach (var pawn in PawnManager.Instance.GetAllPawns())
        {
            if (!pawn.IsAlive) continue;

            Vector2 screenPos = camera.UnprojectPosition(pawn.GlobalPosition);
            if (screenRect.HasPoint(screenPos))
            {
                Select(pawn);
            }
        }
    }

    private void HandleRightClick(Vector2 screenPos)
    {
        if (_selected.Count == 0) return;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        // Raycast from screen to XZ plane (Y=0)
        Vector3 worldTarget = ScreenToWorldXZ(screenPos, camera);

        // Use pathfinding if available
        if (PathfindingService.Instance != null)
        {
            MoveSelectedWithPathfinding(worldTarget);
        }
        else
        {
            // Direct movement fallback
            foreach (var pawn in _selected)
            {
                pawn.MoveTo(worldTarget);
            }
        }
    }

    private void MoveSelectedWithPathfinding(Vector3 worldTarget)
    {
        Vector2I goalBlock = PathfindingService.WorldToBlock(worldTarget);

        for (int i = 0; i < _selected.Count; i++)
        {
            var pawn = _selected[i];
            if (!pawn.IsAlive) continue;

            // Offset goal slightly for multi-unit formations
            Vector2I pawnGoal = goalBlock;
            if (_selected.Count > 1)
            {
                // Simple grid formation
                int row = i / 3;
                int col = i % 3 - 1; // -1, 0, 1
                pawnGoal = new Vector2I(goalBlock.X + col, goalBlock.Y + row);
            }

            Vector2I startBlock = PathfindingService.WorldToBlock(pawn.GlobalPosition);
            var blockPath = PathfindingService.Instance.FindPath(startBlock, pawnGoal);
            var worldPath = PathfindingService.PathToWorld(blockPath);

            if (worldPath != null && worldPath.Count > 0)
            {
                pawn.FollowPath(worldPath);
            }
            else
            {
                // Fallback to direct move
                pawn.MoveTo(worldTarget);
            }
        }
    }

    // --- Selection state ---

    private void Select(Pawn.Pawn pawn)
    {
        if (!_selected.Contains(pawn))
            _selected.Add(pawn);
    }

    private void Deselect(Pawn.Pawn pawn)
    {
        _selected.Remove(pawn);
    }

    private void DeselectAll()
    {
        _selected.Clear();
    }

    private void SelectAll()
    {
        _selected.Clear();
        if (PawnManager.Instance == null) return;
        foreach (var pawn in PawnManager.Instance.GetAllPawns())
        {
            if (pawn.IsAlive) _selected.Add(pawn);
        }
    }

    // --- Utility ---

    private Rect2 GetBoxRect()
    {
        float x = Mathf.Min(_boxStart.X, _boxEnd.X);
        float y = Mathf.Min(_boxStart.Y, _boxEnd.Y);
        float w = Mathf.Abs(_boxEnd.X - _boxStart.X);
        float h = Mathf.Abs(_boxEnd.Y - _boxStart.Y);
        return new Rect2(x, y, w, h);
    }

    private Pawn.Pawn FindPawnAtScreenPos(Vector2 screenPos, Camera3D camera, float maxDist)
    {
        if (PawnManager.Instance == null) return null;

        Pawn.Pawn closest = null;
        float closestDist = maxDist;

        foreach (var pawn in PawnManager.Instance.GetAllPawns())
        {
            if (!pawn.IsAlive) continue;

            Vector2 pawnScreen = camera.UnprojectPosition(pawn.GlobalPosition);
            float dist = pawnScreen.DistanceTo(screenPos);

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = pawn;
            }
        }

        return closest;
    }

    /// <summary>Convert screen position to world XZ plane (Y=0).</summary>
    public static Vector3 ScreenToWorldXZ(Vector2 screenPos, Camera3D camera)
    {
        Vector3 from = camera.ProjectRayOrigin(screenPos);
        Vector3 dir = camera.ProjectRayNormal(screenPos);

        // Intersect with Y=0 plane
        if (Mathf.Abs(dir.Y) < 0.0001f)
            return from;

        float t = -from.Y / dir.Y;
        return from + dir * t;
    }
}
