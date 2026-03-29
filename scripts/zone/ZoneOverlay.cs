using EndfieldZero.Core;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Zone;

/// <summary>
/// Renders zone overlays as semi-transparent colored quads in 3D world.
/// Each zone type has a distinct color.
/// </summary>
public partial class ZoneOverlay : MeshInstance3D
{
    private static ShaderMaterial _overlayMat;

    public override void _Ready()
    {
        MaterialOverride = GetOverlayMaterial();
        CastShadow = ShadowCastingSetting.Off;
    }

    public override void _Process(double delta)
    {
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

    private static ShaderMaterial GetOverlayMaterial()
    {
        if (_overlayMat != null) return _overlayMat;
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
void fragment() { ALBEDO = COLOR.rgb; ALPHA = COLOR.a; }";
        _overlayMat = new ShaderMaterial { Shader = shader };
        return _overlayMat;
    }
}
