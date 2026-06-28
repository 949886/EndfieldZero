using Cherry.World;
using Godot;

namespace Cherry.Pawn;

/// <summary>
/// Stylized soft ground shadow for 3D pawns.
/// Keeps the current flat look while avoiding directional-shadow artifacts.
/// </summary>
public partial class PawnGroundShadow : MeshInstance3D
{
    private const float TopDownYOffset = 0.025f;
    private const float AngledYOffset = 0.03f;
    private const int ShadowRenderPriority = 24;

    [Export(PropertyHint.Range, "0.1,3.0,0.01")] public float Width { get; set; } = 0.92f;
    [Export(PropertyHint.Range, "0.1,3.0,0.01")] public float Height { get; set; } = 0.56f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float Opacity { get; set; } = 0.32f;

    private static ShaderMaterial _shadowMaterial;
    private QuadMesh _quadMesh;

    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
        _quadMesh = new QuadMesh();
        Mesh = _quadMesh;
        RotationDegrees = new Vector3(-90f, 0f, 0f);
        MaterialOverride = GetShadowMaterial();
        UpdatePresentation();
    }

    public override void _Process(double delta)
    {
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        bool angledView = GameCamera.Instance?.IsAngledView == true;
        Visible = angledView;
        Position = new Vector3(0f, angledView ? AngledYOffset : TopDownYOffset, 0f);
        SortingOffset = -0.1f;

        if (_quadMesh != null)
            _quadMesh.Size = new Vector2(Width, Height);

        if (MaterialOverride is ShaderMaterial shader)
            shader.SetShaderParameter("shadow_alpha", Opacity);
    }

    private static ShaderMaterial GetShadowMaterial()
    {
        if (_shadowMaterial != null)
            return _shadowMaterial;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled;

uniform float shadow_alpha = 0.22;

void fragment() {
    vec2 centered = UV * 2.0 - vec2(1.0);
    float dist = length(centered);
    float softness = 1.0 - smoothstep(0.15, 1.0, dist);
    ALBEDO = vec3(0.0);
    ALPHA = shadow_alpha * softness;
}";
        _shadowMaterial = new ShaderMaterial
        {
            Shader = shader,
            RenderPriority = ShadowRenderPriority,
        };
        return _shadowMaterial;
    }
}
