using System.Collections.Generic;
using Godot;

namespace Cherry.Core;

/// <summary>
/// Applies a shared white outline overlay to a bound 3D mesh hierarchy.
/// The original material overlay for each mesh is restored when disabled.
/// </summary>
internal sealed class SelectionOutline3D
{
    private const float DefaultOutlineWidth = 0.03f;
    private const int OutlineRenderPriority = 1;
    private static readonly Color DefaultOutlineColor = Colors.White;

    private static ShaderMaterial _sharedOutlineMaterial;

    private readonly List<GeometryInstance3D> _targets = new();
    private readonly Dictionary<GeometryInstance3D, Material> _previousOverlays = new();

    private Node3D _root;
    private bool _enabled;

    public void Bind(Node3D root)
    {
        if (_root == root)
            return;

        bool reapply = _enabled;
        if (reapply)
            SetEnabled(false);

        _root = root;
        RefreshTargets();

        if (reapply)
            SetEnabled(true);
    }

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
        {
            if (enabled)
                SyncSharedMaterialParameters();
            return;
        }

        _enabled = enabled;

        if (_enabled)
            ApplyOutline();
        else
            RestoreOverlays();
    }

    public void RefreshTargets()
    {
        bool reapply = _enabled;
        if (reapply)
            RestoreOverlays();

        _targets.Clear();
        _previousOverlays.Clear();

        if (_root != null && GodotObject.IsInstanceValid(_root))
            CollectGeometry(_root);

        if (reapply)
            ApplyOutline();
    }

    private void ApplyOutline()
    {
        if (_targets.Count == 0)
            return;

        Material overlay = GetSharedOutlineMaterial();
        foreach (GeometryInstance3D target in _targets)
        {
            if (!GodotObject.IsInstanceValid(target))
                continue;

            _previousOverlays[target] = target.MaterialOverlay;
            target.MaterialOverlay = overlay;
        }
    }

    private void RestoreOverlays()
    {
        foreach ((GeometryInstance3D target, Material overlay) in _previousOverlays)
        {
            if (!GodotObject.IsInstanceValid(target))
                continue;

            target.MaterialOverlay = overlay;
        }

        _previousOverlays.Clear();
    }

    private void CollectGeometry(Node node)
    {
        if (node is GeometryInstance3D geometry)
            _targets.Add(geometry);

        foreach (Node child in node.GetChildren())
            CollectGeometry(child);
    }

    private static ShaderMaterial GetSharedOutlineMaterial()
    {
        if (_sharedOutlineMaterial != null)
        {
            SyncSharedMaterialParameters();
            return _sharedOutlineMaterial;
        }

        Shader shader = new()
        {
            Code = @"
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, shadows_disabled;

uniform vec4 outline_color : source_color = vec4(1.0);
uniform float outline_width = 0.03;
uniform float outline_offset = 0.0;

void vertex() {
    VERTEX += NORMAL * (outline_width + outline_offset);
}

void fragment() {
    ALBEDO = outline_color.rgb;
    ALPHA = outline_color.a;
}"
        };

        _sharedOutlineMaterial = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = OutlineRenderPriority,
        };
        SyncSharedMaterialParameters();
        return _sharedOutlineMaterial;
    }

    private static void SyncSharedMaterialParameters()
    {
        if (_sharedOutlineMaterial == null)
            return;

        float width = Mathf.Max(Settings.SelectionOutlineWidth, 0.001f);
        float offset = Mathf.Max(Settings.SelectionOutlineOffset, 0f);
        Color color = Settings.SelectionOutlineColor;

        _sharedOutlineMaterial.SetShaderParameter("outline_color", color);
        _sharedOutlineMaterial.SetShaderParameter("outline_width", width * Settings.BlockPixelSize);
        _sharedOutlineMaterial.SetShaderParameter("outline_offset", offset * Settings.BlockPixelSize);
    }
}
