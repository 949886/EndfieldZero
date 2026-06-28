using Cherry.Core;
using Cherry.UI;
using Cherry.World;
using Godot;

namespace Cherry.Zone;

/// <summary>
/// Renders zone overlays as semi-transparent colored quads in 3D world.
/// Each zone type has a distinct color.
/// </summary>
public partial class ZoneOverlay : MeshInstance3D
{
    private const int TopDownRenderPriority = -128;
    private const int OverlayRenderPriority = 6;
    private static ShaderMaterial _topDownOverlayMat;
    private static ShaderMaterial _angledOverlayMat;

    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
        ProjectedWorldOverlayHelper.DestroyCanvasByName(this, $"{Name}Projected");
        ApplyMaterialForCurrentView();
    }

    public override void _ExitTree()
    {
        ProjectedWorldOverlayHelper.DestroyCanvasByName(this, $"{Name}Projected");
    }

    public override void _Process(double delta)
    {
        ProjectedWorldOverlayHelper.DestroyCanvasByName(this, $"{Name}Projected");
        Visible = true;
        ApplyMaterialForCurrentView();
        RebuildMesh();
    }

    private void RebuildMesh()
    {
        var system = ZoneSystem.Instance;
        if (system == null || system.AllZones.Count == 0) { Mesh = null; return; }

        float px = Settings.BlockPixelSize;
        var verts = new System.Collections.Generic.List<Vector3>();
        var cols = new System.Collections.Generic.List<Color>();

        foreach (var zone in system.AllZones)
        {
            foreach (var cell in zone.Cells)
            {
                float y = (WorldManager.Instance?.GetSurfaceTopY(cell.X, cell.Y) ?? 0f) + 0.005f;
                float inset = px * 0.03f;
                Vector3 tl = new(cell.X * px + inset, y, cell.Y * px + inset);
                Vector3 tr = new((cell.X + 1) * px - inset, y, cell.Y * px + inset);
                Vector3 br = new((cell.X + 1) * px - inset, y, (cell.Y + 1) * px - inset);
                Vector3 bl = new(cell.X * px + inset, y, (cell.Y + 1) * px - inset);

                var c = zone.OverlayColor;
                verts.Add(tl); cols.Add(c);
                verts.Add(bl); cols.Add(c);
                verts.Add(br); cols.Add(c);
                verts.Add(tl); cols.Add(c);
                verts.Add(br); cols.Add(c);
                verts.Add(tr); cols.Add(c);
            }
        }

        if (verts.Count == 0) { Mesh = null; return; }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = cols.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
    }

    private void ApplyMaterialForCurrentView()
    {
        bool angledView = GameCamera.Instance?.IsAngledView == true;
        MaterialOverride = angledView ? GetAngledOverlayMaterial() : GetTopDownOverlayMaterial();
    }

    private static ShaderMaterial GetTopDownOverlayMaterial()
    {
        if (_topDownOverlayMat != null) return _topDownOverlayMat;
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
void fragment() { ALBEDO = COLOR.rgb; ALPHA = COLOR.a; }";
        _topDownOverlayMat = new ShaderMaterial { Shader = shader, RenderPriority = TopDownRenderPriority };
        return _topDownOverlayMat;
    }

    private static ShaderMaterial GetAngledOverlayMaterial()
    {
        if (_angledOverlayMat != null) return _angledOverlayMat;
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
void fragment() { ALBEDO = COLOR.rgb; ALPHA = COLOR.a; }";
        _angledOverlayMat = new ShaderMaterial { Shader = shader, RenderPriority = OverlayRenderPriority };
        return _angledOverlayMat;
    }
}
