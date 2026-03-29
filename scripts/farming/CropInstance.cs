using EndfieldZero.Core;
using EndfieldZero.Jobs;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Farming;

/// <summary>
/// Runtime crop instance — a planted crop in the world.
/// Rendered as a Sprite3D (billboard) showing the current growth stage.
/// Subscribes to tick events to grow over time.
/// Implements ISelectable for click-to-inspect.
/// </summary>
public partial class CropInstance : Node3D, ISelectable
{
    public CropDef Def { get; private set; }
    public Vector2I BlockCoord { get; private set; }

    // --- Growth ---
    public int CurrentStage { get; private set; }
    public int TicksInStage { get; private set; }
    public bool IsMature => CurrentStage >= Def.GrowthStages - 1;
    public float GrowthProgress => Def.TotalGrowthTicks > 0
        ? (float)(CurrentStage * Def.TicksPerStage + TicksInStage) / Def.TotalGrowthTicks
        : 1f;

    // --- Selection ---
    public bool IsSelected { get; set; }
    public string SelectionTitle => $"{Def.DisplayName} ({(IsMature ? "成熟" : $"生长中 {CurrentStage + 1}/{Def.GrowthStages}")})";
    public string SelectionInfo
    {
        get
        {
            string status = IsMature ? "✅ 已成熟，等待收获" : $"🌱 阶段 {CurrentStage + 1}/{Def.GrowthStages}";
            int ticksLeft = IsMature ? 0 : (Def.TicksPerStage - TicksInStage) + (Def.GrowthStages - 1 - CurrentStage) * Def.TicksPerStage;
            string timeLeft = IsMature ? "" : $"\n⏱️ 剩余: ~{ticksLeft / 60}秒";
            return $"{status}\n📊 进度: {GrowthProgress * 100f:F0}%{timeLeft}\n🌾 可收获: {Def.HarvestYield}";
        }
    }

    // --- Rendering ---
    private Sprite3D _sprite;
    private SelectionCircleNode _selectionCircle;

    private bool _harvestJobCreated;

    /// <summary>Initialize this crop with its definition and position.</summary>
    public void Init(CropDef def, Vector2I blockCoord)
    {
        Def = def;
        BlockCoord = blockCoord;

        float px = Settings.BlockPixelSize;
        float baseY = WorldManager.Instance?.GetSurfaceTopY(blockCoord.X, blockCoord.Y) ?? 0f;
        Position = new Vector3(
            (blockCoord.X + 0.5f) * px,
            baseY + 0.02f,
            (blockCoord.Y + 0.5f) * px
        );
    }

    public override void _Ready()
    {
        // Create Sprite3D
        _sprite = new Sprite3D
        {
            PixelSize = 0.06f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Shaded = false,
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
        };
        AddChild(_sprite);

        // Selection circle
        _selectionCircle = new SelectionCircleNode();
        AddChild(_selectionCircle);
        _selectionCircle.Visible = false;

        // Subscribe to tick
        EventBus.Tick += OnTick;

        UpdateSprite();
        UpdatePresentationMode();
    }

    public override void _ExitTree()
    {
        EventBus.Tick -= OnTick;
    }

    public override void _Process(double delta)
    {
        _selectionCircle.Visible = IsSelected;
        UpdatePresentationMode();
    }

    private void OnTick(long tick)
    {
        if (IsMature)
        {
            // Create harvest job once
            if (!_harvestJobCreated)
            {
                _harvestJobCreated = true;
                CreateHarvestJob();
            }
            return;
        }

        TicksInStage++;
        if (TicksInStage >= Def.TicksPerStage)
        {
            TicksInStage = 0;
            CurrentStage++;
            UpdateSprite();

            if (IsMature)
            {
                GD.Print($"[Crop] {Def.DisplayName} at {BlockCoord} is mature!");
            }
        }
    }

    private void UpdateSprite()
    {
        if (_sprite == null) return;
        _sprite.Texture = Def.GetStageTexture(CurrentStage);
        UpdateSpriteAnchor();
    }

    private void UpdatePresentationMode()
    {
        if (_sprite == null)
            return;

        bool angled3D = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;
        _sprite.NoDepthTest = angled3D;
        _sprite.RenderPriority = angled3D ? 8 : 0;
        _sprite.AlphaCut = angled3D
            ? SpriteBase3D.AlphaCutMode.Disabled
            : SpriteBase3D.AlphaCutMode.OpaquePrepass;
        UpdateSpriteAnchor();
    }

    private void UpdateSpriteAnchor()
    {
        if (_sprite?.Texture == null)
            return;

        float halfHeight = _sprite.Texture.GetHeight() * _sprite.PixelSize * 0.5f;
        _sprite.Position = new Vector3(0f, halfHeight, 0f);
    }

    private void CreateHarvestJob()
    {
        if (JobSystem.Instance == null) return;

        float px = Settings.BlockPixelSize;
        var job = new Job("Harvest", $"收获{Def.DisplayName}")
        {
            TargetBlockCoord = BlockCoord,
            TargetWorldPos = Position,
            RequiredSkill = "Growing",
            MinSkillLevel = 0f,
            WorkTicks = 180,
            BasePriority = 6,
            XpPerTick = Def.XpPerTick,
        };
        JobSystem.Instance.AddJob(job);
    }
}

/// <summary>
/// Simple green ring for selection (used by crops and buildings).
/// Same visual as pawn SelectionCircle but as a separate reusable node.
/// </summary>
public partial class SelectionCircleNode : MeshInstance3D
{
    private static ShaderMaterial _mat;

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.01f, 0f);
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = GetMaterial();
        BuildRingMesh(0.15f, 0.22f, 32);
    }

    private void BuildRingMesh(float innerR, float outerR, int segments)
    {
        var im = new ImmediateMesh();
        im.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            im.SurfaceSetColor(new Color(0.3f, 1f, 0.3f, 0.7f));
            im.SurfaceAddVertex(new Vector3(cos * outerR, 0f, sin * outerR));
            im.SurfaceAddVertex(new Vector3(cos * innerR, 0f, sin * innerR));
        }

        im.SurfaceEnd();
        Mesh = im;
    }

    private static ShaderMaterial GetMaterial()
    {
        if (_mat != null) return _mat;
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled;
void fragment() { ALBEDO = COLOR.rgb; ALPHA = COLOR.a; }";
        _mat = new ShaderMaterial { Shader = shader };
        return _mat;
    }
}
