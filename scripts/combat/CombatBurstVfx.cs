using System.Collections.Generic;
using Godot;

namespace Cherry.Combat;

public partial class CombatBurstVfx : Node3D
{
    [Export] public Color BaseColor { get; set; } = new(1f, 0.9f, 0.5f, 1f);
    [Export(PropertyHint.Range, "0.02,1.0,0.01")] public float LifetimeSeconds { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0.01,2.0,0.01")] public float StartScale { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0.01,3.0,0.01")] public float EndScale { get; set; } = 0.8f;
    [Export] public bool GroundAligned { get; set; }
    [Export(PropertyHint.Range, "0,8,1")] public int RayCount { get; set; } = 4;
    [Export(PropertyHint.Range, "0.01,2.0,0.01")] public float RayLength { get; set; } = 0.3f;
    [Export(PropertyHint.Range, "0.01,1.0,0.01")] public float CoreThickness { get; set; } = 0.06f;

    private readonly List<MeshInstance3D> _visuals = new();
    private double _elapsed;
    private StandardMaterial3D _coreMaterial;
    private StandardMaterial3D _rayMaterial;

    public override void _Ready()
    {
        if (GroundAligned)
            RotationDegrees = new Vector3(-90f, RotationDegrees.Y, RotationDegrees.Z);

        BuildVisuals();
        ApplyVisualState(0f);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        float t = LifetimeSeconds <= 0.0001f ? 1f : Mathf.Clamp((float)(_elapsed / LifetimeSeconds), 0f, 1f);
        ApplyVisualState(t);

        if (_elapsed >= LifetimeSeconds)
            QueueFree();
    }

    public void ApplyTint(Color color)
    {
        BaseColor = color;
        ApplyMaterialColor(_coreMaterial, color, 1f);
        ApplyMaterialColor(_rayMaterial, color, 0.85f);
    }

    private void BuildVisuals()
    {
        var core = new MeshInstance3D();
        core.Mesh = GroundAligned ? CreateDiscMesh() : CreateCoreMesh();
        _coreMaterial = CreateMaterial(BaseColor, 1f);
        core.MaterialOverride = _coreMaterial;
        AddChild(core);
        _visuals.Add(core);

        if (RayCount <= 0)
            return;

        _rayMaterial = CreateMaterial(BaseColor, 0.85f);
        for (int i = 0; i < RayCount; i++)
        {
            var ray = new MeshInstance3D();
            ray.Mesh = CreateRayMesh();
            ray.MaterialOverride = _rayMaterial;
            float angle = 360f * i / Mathf.Max(1, RayCount);
            ray.RotationDegrees = GroundAligned
                ? new Vector3(0f, 0f, angle)
                : new Vector3(0f, angle, 0f);
            AddChild(ray);
            _visuals.Add(ray);
        }
    }

    private void ApplyVisualState(float t)
    {
        float scale = Mathf.Lerp(StartScale, EndScale, t);
        Scale = Vector3.One * scale;

        float alpha = 1f - t;
        ApplyMaterialColor(_coreMaterial, BaseColor, alpha);
        ApplyMaterialColor(_rayMaterial, BaseColor, alpha * 0.85f);
    }

    private static StandardMaterial3D CreateMaterial(Color color, float alpha)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            VertexColorUseAsAlbedo = true,
            EmissionEnabled = true,
        };
        ApplyMaterialColor(material, color, alpha);
        return material;
    }

    private static void ApplyMaterialColor(StandardMaterial3D material, Color color, float alpha)
    {
        if (material == null)
            return;

        Color final = new(color.R, color.G, color.B, Mathf.Clamp(alpha, 0f, 1f));
        material.AlbedoColor = final;
        material.Emission = new Color(color.R, color.G, color.B, 1f);
    }

    private Mesh CreateCoreMesh()
    {
        return new SphereMesh
        {
            Radius = Mathf.Max(0.02f, CoreThickness),
            Height = Mathf.Max(0.04f, CoreThickness * 2f),
        };
    }

    private Mesh CreateDiscMesh()
    {
        return new CylinderMesh
        {
            TopRadius = Mathf.Max(0.02f, CoreThickness * 2f),
            BottomRadius = Mathf.Max(0.02f, CoreThickness * 2f),
            Height = Mathf.Max(0.01f, CoreThickness * 0.4f),
        };
    }

    private Mesh CreateRayMesh()
    {
        return new BoxMesh
        {
            Size = new Vector3(
                Mathf.Max(0.015f, CoreThickness * 0.45f),
                Mathf.Max(0.015f, CoreThickness * 0.45f),
                Mathf.Max(0.08f, RayLength))
        };
    }
}
