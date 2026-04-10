using System.Collections.Generic;
using Godot;

namespace EndfieldZero.UI;

public readonly struct ProjectedOverlayPolygon
{
    public ProjectedOverlayPolygon(Vector2[] points, Color color)
    {
        Points = points;
        Color = color;
    }

    public Vector2[] Points { get; }
    public Color Color { get; }
}

public partial class ProjectedWorldOverlayCanvas : Control
{
    private ProjectedOverlayPolygon[] _polygons = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = -1;
        Visible = false;
        UpdateViewportRect();
    }

    public override void _Process(double delta)
    {
        UpdateViewportRect();
    }

    public void SetPolygons(List<ProjectedOverlayPolygon> polygons)
    {
        _polygons = polygons.Count == 0 ? [] : polygons.ToArray();
        Visible = _polygons.Length > 0;
        QueueRedraw();
    }

    public void Clear()
    {
        if (_polygons.Length == 0 && !Visible)
            return;

        _polygons = [];
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var polygon in _polygons)
            DrawColoredPolygon(polygon.Points, polygon.Color);
    }

    private void UpdateViewportRect()
    {
        var rect = GetViewportRect();
        Position = Vector2.Zero;
        Size = rect.Size;
    }
}

public static class ProjectedWorldOverlayHelper
{
    public static void DestroyCanvasByName(Node owner, string name)
    {
        if (owner == null || owner.GetTree() == null)
            return;

        var sceneRoot = owner.GetTree().CurrentScene ?? owner.GetTree().Root;
        if (sceneRoot == null)
            return;

        var uiLayer = sceneRoot.GetNodeOrNull<CanvasLayer>("UILayer");
        uiLayer?.GetNodeOrNull<Control>(name)?.QueueFree();
        sceneRoot.GetNodeOrNull<CanvasLayer>($"{name}Layer")?.QueueFree();
    }

    public static ProjectedWorldOverlayCanvas CreateCanvas(Node owner, string name, int layer = 0)
    {
        var sceneRoot = owner.GetTree().CurrentScene ?? owner.GetTree().Root;
        var uiLayer = sceneRoot.GetNodeOrNull<CanvasLayer>("UILayer");
        var canvas = new ProjectedWorldOverlayCanvas
        {
            Name = name,
        };

        if (uiLayer != null)
        {
            uiLayer.AddChild(canvas);
            return canvas;
        }

        var canvasLayer = new CanvasLayer
        {
            Name = $"{name}Layer",
            Layer = layer,
        };

        sceneRoot.AddChild(canvasLayer);
        canvasLayer.AddChild(canvas);
        return canvas;
    }

    public static void DestroyCanvas(ProjectedWorldOverlayCanvas canvas)
    {
        if (canvas == null)
            return;

        if (canvas.GetParent() is CanvasLayer canvasLayer && canvasLayer.Name == $"{canvas.Name}Layer")
        {
            canvasLayer.QueueFree();
            return;
        }

        canvas.QueueFree();
    }

    public static bool TryProjectQuad(
        Camera3D camera,
        Vector3 tl,
        Vector3 tr,
        Vector3 br,
        Vector3 bl,
        out Vector2[] points)
    {
        points = null;
        if (camera == null)
            return false;

        points =
        [
            camera.UnprojectPosition(tl),
            camera.UnprojectPosition(tr),
            camera.UnprojectPosition(br),
            camera.UnprojectPosition(bl),
        ];

        return IsFinite(points[0])
            && IsFinite(points[1])
            && IsFinite(points[2])
            && IsFinite(points[3]);
    }

    private static bool IsFinite(Vector2 point)
    {
        return float.IsFinite(point.X) && float.IsFinite(point.Y);
    }
}
