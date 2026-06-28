using Cherry.World;
using Godot;

namespace Cherry.Environment;

/// <summary>
/// Adds a subtle directional light and ambient environment for angled 3D views
/// without breaking the project's flat-shaded look.
/// </summary>
public partial class SceneLightingRig : Node3D
{
    [Export] public Color AmbientColor { get; set; } = new(0.96f, 0.98f, 1f);
    [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float AmbientEnergy { get; set; } = 0.85f;
    [Export] public Color KeyLightColor { get; set; } = new(1f, 0.98f, 0.94f);
    [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float AngledViewLightEnergy { get; set; } = 0.38f;
    [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float TopDownLightEnergy { get; set; } = 0f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float ShadowOpacity { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0.0,4.0,0.01")] public float ShadowBlur { get; set; } = 0.8f;
    [Export(PropertyHint.Range, "0.0,5.0,0.01")] public float LightAngularDistance { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "32.0,4096.0,1.0")] public float ShadowMaxDistance { get; set; } = 700f;
    [Export] public Vector3 KeyLightRotationDegrees { get; set; } = new(-48f, -40f, 0f);

    private DirectionalLight3D _keyLight;
    private WorldEnvironment _worldEnvironment;
    private float _lastAppliedEnergy = float.NaN;
    private bool _lastShadowEnabled;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _keyLight = EnsureKeyLight();
        _worldEnvironment = EnsureWorldEnvironment();

        ConfigureEnvironment();
        ConfigureLight();
        ApplyViewState(force: true);
    }

    public override void _Process(double delta)
    {
        ApplyViewState(force: false);
    }

    private DirectionalLight3D EnsureKeyLight()
    {
        if (GetNodeOrNull<DirectionalLight3D>("KeyLight") is { } existing)
            return existing;

        var light = new DirectionalLight3D
        {
            Name = "KeyLight",
        };
        AddChild(light);
        return light;
    }

    private WorldEnvironment EnsureWorldEnvironment()
    {
        if (GetNodeOrNull<WorldEnvironment>("WorldEnvironment") is { } existing)
            return existing;

        var environment = new WorldEnvironment
        {
            Name = "WorldEnvironment",
        };
        AddChild(environment);
        return environment;
    }

    private void ConfigureEnvironment()
    {
        if (_worldEnvironment == null)
            return;

        _worldEnvironment.Environment ??= new Godot.Environment();
        _worldEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.ClearColor;
        _worldEnvironment.Environment.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        _worldEnvironment.Environment.AmbientLightColor = AmbientColor;
        _worldEnvironment.Environment.AmbientLightSkyContribution = 0f;
        _worldEnvironment.Environment.AmbientLightEnergy = AmbientEnergy;
        _worldEnvironment.Environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
    }

    private void ConfigureLight()
    {
        if (_keyLight == null)
            return;

        _keyLight.RotationDegrees = KeyLightRotationDegrees;
        _keyLight.LightColor = KeyLightColor;
        _keyLight.ShadowOpacity = ShadowOpacity;
        _keyLight.ShadowBlur = ShadowBlur;
        _keyLight.LightAngularDistance = LightAngularDistance;
        _keyLight.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        _keyLight.DirectionalShadowBlendSplits = true;
        _keyLight.DirectionalShadowMaxDistance = ShadowMaxDistance;
    }

    private void ApplyViewState(bool force)
    {
        if (_keyLight == null)
            return;

        bool angledView = GameCamera.Instance?.IsAngledView ?? true;
        float targetEnergy = angledView ? AngledViewLightEnergy : TopDownLightEnergy;
        bool enableShadows = false;

        if (!force
            && Mathf.IsEqualApprox(_lastAppliedEnergy, targetEnergy)
            && _lastShadowEnabled == enableShadows)
        {
            return;
        }

        _keyLight.LightEnergy = targetEnergy;
        _keyLight.ShadowEnabled = enableShadows;
        _lastAppliedEnergy = targetEnergy;
        _lastShadowEnabled = enableShadows;
    }
}
