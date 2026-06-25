using Godot;
using EndfieldZero.World;
using EndfieldZero.UI;

namespace EndfieldZero.Building;

/// <summary>
/// Renders blueprint ghost overlays in 3D world.
/// Shows semi-transparent quads on blueprint cells:
///   Blue  = queued, waiting for pawn
///   Cyan  = pawn is building (pulses)
///   Red   = invalid placement preview
///
/// Also renders the placement preview when the player is hovering in Construct mode.
/// </summary>
public partial class BlueprintOverlay : MeshInstance3D
{
    private const int TopDownRenderPriority = -128;
    private const int OverlayRenderPriority = 6;
    private static ShaderMaterial _topDownOverlayMat;
    private static ShaderMaterial _angledOverlayMat;

    // Preview state (set by ToolModeManager)
    public BuildingDef PreviewDef { get; set; }
    public Vector2I PreviewCoord { get; set; }
    public int PreviewRotation { get; set; }
    public bool ShowPreview { get; set; }

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
        var system = BlueprintSystem.Instance;
        float px = Core.Settings.BlockPixelSize;

        var vertices = new System.Collections.Generic.List<Vector3>();
        var colors = new System.Collections.Generic.List<Color>();

        // Render existing blueprints
        if (system != null)
        {
            foreach (var bp in system.AllBlueprints)
            {
                if (bp.Status == BlueprintStatus.Complete) continue;

                Color color;
                if (bp.Status == BlueprintStatus.Building)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.005f);
                    color = new Color(0.3f, 0.8f, 1f, 0.25f + 0.2f * pulse);
                }
                else
                {
                    color = bp.Def.GhostColor;
                }

                foreach (var cell in bp.OccupiedCells())
                {
                    AddQuad(vertices, colors, cell, px, color);
                }
            }
        }

        // Render placement preview
        if (ShowPreview && PreviewDef != null)
        {
            var tempBp = new Blueprint(PreviewDef, PreviewCoord, PreviewRotation);
            bool canPlace = system?.CanPlace(tempBp) ?? false;

            Color previewColor = canPlace
                ? new Color(0.3f, 0.8f, 0.4f, 0.35f)  // Green = valid
                : new Color(1f, 0.3f, 0.2f, 0.35f);    // Red = invalid

            foreach (var cell in tempBp.OccupiedCells())
            {
                AddQuad(vertices, colors, cell, px, previewColor);
            }
        }

        if (vertices.Count == 0) { Mesh = null; return; }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
    }

    private static void AddQuad(
        System.Collections.Generic.List<Vector3> verts,
        System.Collections.Generic.List<Color> cols,
        Vector2I cell, float px, Color color)
    {
        float y = (WorldManager.Instance?.GetSurfaceTopY(cell.X, cell.Y) ?? 0f) + 0.015f;
        float inset = px * 0.02f;
        Vector3 tl = new(cell.X * px + inset, y, cell.Y * px + inset);
        Vector3 tr = new((cell.X + 1) * px - inset, y, cell.Y * px + inset);
        Vector3 br = new((cell.X + 1) * px - inset, y, (cell.Y + 1) * px - inset);
        Vector3 bl = new(cell.X * px + inset, y, (cell.Y + 1) * px - inset);

        verts.Add(tl); cols.Add(color);
        verts.Add(bl); cols.Add(color);
        verts.Add(br); cols.Add(color);
        verts.Add(tl); cols.Add(color);
        verts.Add(br); cols.Add(color);
        verts.Add(tr); cols.Add(color);
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
