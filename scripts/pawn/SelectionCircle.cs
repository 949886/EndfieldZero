using Godot;

namespace EndfieldZero.Pawn;

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
    [Export] public float OuterRadius { get; set; } = 0.35f;
    [Export] public float InnerRadius { get; set; } = 0.28f;
    [Export] public int Segments { get; set; } = 32;

    private static ShaderMaterial _circleMat;
    private bool _wasVisible;

    public override void _Ready()
    {
        // Position slightly above ground so it's visible
        Position = new Vector3(0f, 0.03f, 0f);
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = GetCircleMaterial();

        // Start hidden
        Visible = false;
        BuildRingMesh();
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

    private static ShaderMaterial GetCircleMaterial()
    {
        if (_circleMat != null) return _circleMat;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled;

void fragment() {
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}";
        _circleMat = new ShaderMaterial { Shader = shader };
        return _circleMat;
    }
}
