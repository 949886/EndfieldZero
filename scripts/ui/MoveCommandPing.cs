using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Short-lived green ping effect at right-click move target.
/// Spawned by SelectionManager when issuing move commands.
/// Renders a shrinking ring that fades out over 0.5 seconds.
/// </summary>
public partial class MoveCommandPing : MeshInstance3D
{
    private float _life;
    private const float Duration = 0.6f;
    private const float MaxRadius = 0.6f;
    private const int Segments = 24;

    private static ShaderMaterial _pingMat;

    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = GetPingMaterial();
        Position = new Vector3(Position.X, Position.Y + 0.02f, Position.Z);
        _life = Duration;
        BuildMesh(MaxRadius, 0.8f);
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        if (_life <= 0f)
        {
            QueueFree();
            return;
        }

        float t = 1f - (_life / Duration);  // 0 → 1

        // Shrink ring + fade out
        float radius = MaxRadius * (1f - t * 0.6f);
        float alpha = 1f - t;

        BuildMesh(radius, alpha);
    }

    private void BuildMesh(float radius, float alpha)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);

        float inner = radius * 0.7f;
        Color color = new(0.3f, 1f, 0.4f, alpha * 0.6f);

        for (int i = 0; i <= Segments; i++)
        {
            float angle = Mathf.Tau * i / Segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(new Vector3(cos * radius, 0f, sin * radius));

            mesh.SurfaceSetColor(color with { A = alpha * 0.3f });
            mesh.SurfaceAddVertex(new Vector3(cos * inner, 0f, sin * inner));
        }

        mesh.SurfaceEnd();
        Mesh = mesh;
    }

    private static ShaderMaterial GetPingMaterial()
    {
        if (_pingMat != null) return _pingMat;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

void fragment() {
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}";
        _pingMat = new ShaderMaterial { Shader = shader };
        return _pingMat;
    }
}
