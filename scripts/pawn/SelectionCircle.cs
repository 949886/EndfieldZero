using Cherry.World;
using Godot;

namespace Cherry.Pawn;

/// <summary>
/// Green selection circle rendered on the XZ plane under a pawn.
/// Rendered at Y = -0.01 so it appears BELOW the pawn sprite (which uses billboard mode).
/// Uses a ring mesh (annulus) via ImmediateMesh for a clean outline effect.
///
/// Attach as a child of the Pawn CharacterBody3D.
/// Visibility toggled by Pawn.IsSelected.
/// </summary>
public partial class SelectionCircle : MeshInstance3D
{
    private const int TopDownCircleRenderPriority = -128;
    private const int CircleRenderPriority = 7;
    private static readonly Vector3 TopDownOffset = new(0f, 0.03f, 0f);
    private static readonly Vector3 AngledOffset = new(0f, 0.7f, 0f);

    [Export] public float OuterRadius { get; set; } = 0.35f;
    [Export] public float InnerRadius { get; set; } = 0.28f;
    [Export] public int Segments { get; set; } = 32;

    private static ShaderMaterial _topDownCircleMat;
    private static ShaderMaterial _angledCircleMat;
    private bool _wasVisible;

    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
        ApplyMaterialForCurrentView();

        // Start hidden
        Visible = false;
        BuildRingMesh();
    }

    public override void _Process(double delta)
    {
        ApplyMaterialForCurrentView();
    }

    /// <summary>Show or hide the selection circle.</summary>
    public void SetSelected(bool selected)
    {
        if (selected == _wasVisible) return;
        _wasVisible = selected;
        Visible = selected;
    }

    private void BuildRingMesh()
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);

        // Green selection color
        mesh.SurfaceSetColor(new Color(0.2f, 1f, 0.3f, 0.7f));

        for (int i = 0; i <= Segments; i++)
        {
            float angle = Mathf.Tau * i / Segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Outer vertex
            mesh.SurfaceSetColor(new Color(0.2f, 1f, 0.3f, 0.7f));
            mesh.SurfaceAddVertex(new Vector3(cos * OuterRadius, 0f, sin * OuterRadius));

            // Inner vertex
            mesh.SurfaceSetColor(new Color(0.3f, 1f, 0.4f, 0.5f));
            mesh.SurfaceAddVertex(new Vector3(cos * InnerRadius, 0f, sin * InnerRadius));
        }

        mesh.SurfaceEnd();
        Mesh = mesh;
    }

    private void ApplyMaterialForCurrentView()
    {
        bool angledView = GameCamera.Instance?.IsAngledView == true;
        Position = angledView ? AngledOffset : TopDownOffset;
        SortingOffset = angledView ? 0.25f : 0f;
        MaterialOverride = angledView ? GetAngledCircleMaterial() : GetTopDownCircleMaterial();
    }

    private static ShaderMaterial GetTopDownCircleMaterial()
    {
        if (_topDownCircleMat != null) return _topDownCircleMat;
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

void fragment() {
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}";
        _topDownCircleMat = new ShaderMaterial { Shader = shader, RenderPriority = TopDownCircleRenderPriority };
        return _topDownCircleMat;
    }

    private static ShaderMaterial GetAngledCircleMaterial()
    {
        if (_angledCircleMat != null) return _angledCircleMat;
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled;

void fragment() {
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}";
        _angledCircleMat = new ShaderMaterial { Shader = shader, RenderPriority = CircleRenderPriority };
        return _angledCircleMat;
    }
}
