using System.Collections.Generic;
using EndfieldZero.Pawn;
using Godot;

namespace EndfieldZero.Combat;

public partial class CombatProjectile : Node3D
{
    [Export(PropertyHint.Range, "0.01,1.0,0.01")] public float DefaultProjectileRadius { get; set; } = 0.11f;
    [Export(PropertyHint.Range, "0.01,0.5,0.01")] public float DefaultTrailWidth { get; set; } = 0.2f;
    [Export(PropertyHint.Range, "6,32,1")] public int MaxTrailPoints { get; set; } = 18;

    private readonly List<Vector3> _trailPoints = new();
    private PreparedRangedShot _shot;
    private CharacterDefinition _definition;
    private CombatVfxManager _manager;
    private MeshInstance3D _coreVisual;
    private MeshInstance3D _glowVisual;
    private MeshInstance3D _trailVisual;
    private ImmediateMesh _trailMesh;
    private StandardMaterial3D _trailMaterial;
    private Vector3 _impactPoint;
    private float _speed;
    private float _maxLifetime;
    private float _projectileSize;
    private float _trailWidth;
    private Color _trailColor = new(0.78f, 1f, 0.35f, 1f);
    private Vector3 _spawnOrigin;
    private bool _initialized;
    private bool _resolved;
    private double _elapsed;

    private static readonly Texture2D GlowTexture = CreateGlowTexture();

    public void Initialize(
        PreparedRangedShot shot,
        CharacterDefinition definition,
        CombatVfxManager manager,
        Vector3 origin)
    {
        _shot = shot;
        _definition = definition;
        _manager = manager;
        _impactPoint = shot?.ImpactPoint ?? origin;
        _speed = Mathf.Max(1f, definition?.ProjectileSpeed ?? 48f);
        _maxLifetime = Mathf.Max(0.1f, definition?.ProjectileMaxLifetime ?? 1.5f);
        _projectileSize = Mathf.Max(0.03f, definition?.ProjectileSize ?? DefaultProjectileRadius);
        _trailWidth = Mathf.Max(0.02f, definition?.ProjectileTrailWidth ?? DefaultTrailWidth);
        _trailColor = definition?.ProjectileColor ?? _trailColor;
        _spawnOrigin = origin;
        _initialized = true;
    }

    public override void _Ready()
    {
        if (!_initialized)
        {
            _spawnOrigin = GlobalPosition;
            _impactPoint = GlobalPosition;
            _speed = 48f;
            _maxLifetime = 1.5f;
            _projectileSize = DefaultProjectileRadius;
            _trailWidth = DefaultTrailWidth;
        }

        GlobalPosition = _spawnOrigin;
        BuildVisuals();
        _trailPoints.Add(GlobalPosition);
        RebuildTrail();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_resolved)
            return;

        _elapsed += delta;
        if (_elapsed >= _maxLifetime)
        {
            ResolveImpact();
            return;
        }

        Vector3 next = GlobalPosition.MoveToward(_impactPoint, _speed * (float)delta);
        Vector3 movement = next - GlobalPosition;
        if (movement.LengthSquared() > 0.000001f)
            GlobalPosition = next;

        AddTrailPoint(GlobalPosition);
        RebuildTrail();

        if (GlobalPosition.DistanceTo(_impactPoint) <= Mathf.Max(0.08f, _projectileSize * 1.2f))
            ResolveImpact();
    }

    private void BuildVisuals()
    {
        _coreVisual = CreateGlowQuad(
            "Core",
            new Vector2(_projectileSize * 2.2f, _projectileSize * 2.2f),
            _trailColor.Lightened(0.15f),
            1f,
            12);
        AddChild(_coreVisual);

        _glowVisual = CreateGlowQuad(
            "Glow",
            new Vector2(_projectileSize * 4.6f, _projectileSize * 4.6f),
            _trailColor.Lightened(0.05f),
            0.72f,
            11);
        AddChild(_glowVisual);

        _trailMesh = new ImmediateMesh();
        _trailVisual = new MeshInstance3D
        {
            Name = "Trail",
            Mesh = _trailMesh,
        };
        _trailMaterial = CreateTrailMaterial(_trailColor, 10);
        _trailVisual.MaterialOverride = _trailMaterial;
        AddChild(_trailVisual);
    }

    private MeshInstance3D CreateGlowQuad(string name, Vector2 size, Color color, float alpha, int renderPriority)
    {
        var quad = new MeshInstance3D
        {
            Name = name,
            Mesh = new QuadMesh { Size = size },
        };
        quad.MaterialOverride = CreateGlowMaterial(color, alpha, renderPriority);
        return quad;
    }

    private StandardMaterial3D CreateGlowMaterial(Color color, float alpha, int renderPriority)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            EmissionEnabled = true,
            AlbedoTexture = GlowTexture,
            AlbedoColor = new Color(color.R, color.G, color.B, alpha),
            Emission = new Color(color.R, color.G, color.B, 1f),
            RenderPriority = renderPriority,
        };
    }

    private StandardMaterial3D CreateTrailMaterial(Color color, int renderPriority)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            VertexColorUseAsAlbedo = true,
            EmissionEnabled = true,
            Emission = new Color(color.R, color.G, color.B, 1f),
            RenderPriority = renderPriority,
        };
    }

    private void AddTrailPoint(Vector3 point)
    {
        if (_trailPoints.Count > 0 && _trailPoints[^1].DistanceTo(point) < 0.03f)
            return;

        _trailPoints.Add(point);
        if (_trailPoints.Count > MaxTrailPoints)
            _trailPoints.RemoveAt(0);
    }

    private void RebuildTrail()
    {
        if (_trailMesh == null)
            return;

        _trailMesh.ClearSurfaces();
        if (_trailPoints.Count < 2)
            return;

        Camera3D camera = GetViewport()?.GetCamera3D();
        Vector3 viewDirection = camera != null
            ? (camera.GlobalPosition - GlobalPosition).Normalized()
            : Vector3.Up;

        _trailMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        for (int i = 1; i < _trailPoints.Count; i++)
        {
            Vector3 from = _trailPoints[i - 1];
            Vector3 to = _trailPoints[i];
            Vector3 segment = to - from;
            if (segment.LengthSquared() <= 0.000001f)
                continue;

            float t0 = (float)(i - 1) / (_trailPoints.Count - 1);
            float t1 = (float)i / (_trailPoints.Count - 1);

            float width0 = GetRibbonWidth(t0);
            float width1 = GetRibbonWidth(t1);
            Color color0 = GetRibbonColor(t0);
            Color color1 = GetRibbonColor(t1);

            Vector3 side = segment.Normalized().Cross(viewDirection);
            if (side.LengthSquared() <= 0.000001f)
                side = Vector3.Up.Cross(segment.Normalized());
            if (side.LengthSquared() <= 0.000001f)
                side = Vector3.Right;
            side = side.Normalized();

            Vector3 fromLeft = from - side * width0;
            Vector3 fromRight = from + side * width0;
            Vector3 toLeft = to - side * width1;
            Vector3 toRight = to + side * width1;

            AddTrailVertex(ToLocal(fromLeft), color0);
            AddTrailVertex(ToLocal(fromRight), color0);
            AddTrailVertex(ToLocal(toLeft), color1);

            AddTrailVertex(ToLocal(fromRight), color0);
            AddTrailVertex(ToLocal(toRight), color1);
            AddTrailVertex(ToLocal(toLeft), color1);
        }

        _trailMesh.SurfaceEnd();
    }

    private float GetRibbonWidth(float t)
    {
        float tapered = 1f - t;
        tapered *= tapered;
        return Mathf.Lerp(_trailWidth * 0.18f, _trailWidth * 0.95f, tapered);
    }

    private Color GetRibbonColor(float t)
    {
        float alpha = Mathf.Lerp(0.03f, 0.72f, 1f - t);
        float brighten = Mathf.Lerp(0.05f, 0.28f, 1f - t);
        Color color = _trailColor.Lightened(brighten);
        color.A = alpha;
        return color;
    }

    private void AddTrailVertex(Vector3 position, Color color)
    {
        _trailMesh.SurfaceSetColor(color);
        _trailMesh.SurfaceAddVertex(position);
    }

    private void ResolveImpact()
    {
        if (_resolved)
            return;

        _resolved = true;

        bool applied = _shot?.TryApply(out _) == true;
        _manager?.SpawnImpact(_definition, applied, _impactPoint);
        QueueFree();
    }

    private static Texture2D CreateGlowTexture()
    {
        const int size = 128;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        Vector2 center = new(size / 2f, size / 2f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x, y).DistanceTo(center) / radius;
                float alpha = 1f - Mathf.Clamp(distance, 0f, 1f);
                alpha = alpha * alpha * alpha;
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
