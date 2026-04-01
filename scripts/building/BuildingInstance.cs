using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Farming;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Building;

/// <summary>
/// Runtime building entity for furniture and production buildings.
/// In angled view the important buildings use simple blocky 3D meshes.
/// </summary>
public partial class BuildingInstance : Node3D, ISelectable
{
    public BuildingDef Def { get; private set; }
    public Vector2I BlockCoord { get; private set; }
    public int BuildRotation { get; private set; }
    public Vector2I EffectiveSize => BuildRotation % 2 == 1
        ? new Vector2I(Def.Size.Y, Def.Size.X)
        : Def.Size;

    public bool IsSelected { get; set; }
    public string SelectionTitle => Def.DisplayName;
    public string SelectionInfo
    {
        get
        {
            string info = $"馃摝 绫诲埆: {CategoryName(Def.Category)}";
            info += $"\n馃搻 灏哄: {Def.Size.X}脳{Def.Size.Y}";

            if (Def.SatisfiesNeed != null)
                info += $"\n馃挙 婊¤冻闇€姹? {Def.SatisfiesNeed}";
            if (Def.ComfortOffset > 0)
                info += $"\n馃泲锔?鑸掗€? +{Def.ComfortOffset:F0}";
            if (Def.BeautyOffset > 0)
                info += $"\n馃尭 缇庢劅: +{Def.BeautyOffset:F0}";

            return info;
        }
    }

    private Node3D _billboardRoot;
    private Node3D _meshRoot;
    private Sprite3D _billboardSprite;
    private SelectionCircleNode _selectionCircle;
    private readonly List<GeometryInstance3D> _occlusionParts = new();
    private bool _hasAuthoredSprite;

    public void Init(BuildingDef def, Vector2I blockCoord, int rotation = 0)
    {
        Def = def;
        BlockCoord = blockCoord;
        BuildRotation = rotation;

        float px = Settings.BlockPixelSize;
        var effSize = EffectiveSize;

        Position = new Vector3(
            (blockCoord.X + effSize.X * 0.5f) * px,
            ResolveBaseHeight() + 0.01f,
            (blockCoord.Y + effSize.Y * 0.5f) * px);
        RotationDegrees = new Vector3(0f, BuildRotation * 90f, 0f);
    }

    public IEnumerable<Vector2I> OccupiedCells()
    {
        var size = EffectiveSize;
        for (int dz = 0; dz < size.Y; dz++)
        {
            for (int dx = 0; dx < size.X; dx++)
                yield return new Vector2I(BlockCoord.X + dx, BlockCoord.Y + dz);
        }
    }

    public override void _Ready()
    {
        _billboardRoot = new Node3D();
        AddChild(_billboardRoot);

        _meshRoot = new Node3D();
        AddChild(_meshRoot);

        BuildVisual();
        UpdatePresentationMode();

        _selectionCircle = new SelectionCircleNode();
        AddChild(_selectionCircle);
        _selectionCircle.Visible = false;
    }

    public override void _Process(double delta)
    {
        _selectionCircle.Visible = IsSelected;
        UpdatePresentationMode();
        UpdateOcclusion();
    }

    private float ResolveBaseHeight()
    {
        if (WorldManager.Instance == null)
            return 0f;

        float baseHeight = 0f;
        foreach (var cell in OccupiedCells())
            baseHeight = Mathf.Max(baseHeight, WorldManager.Instance.GetSurfaceTopY(cell.X, cell.Y));
        return baseHeight;
    }

    private void BuildVisual()
    {
        BuildBillboardVisual();

        if (Def.View3DStyle == BuildingView3DStyle.Billboard)
            return;

        switch (Def.View3DStyle)
        {
            case BuildingView3DStyle.Bed:
                BuildBedVisual();
                break;
            case BuildingView3DStyle.Table:
                BuildTableVisual();
                break;
            case BuildingView3DStyle.Workstation:
                BuildWorkstationVisual();
                break;
            case BuildingView3DStyle.Stove:
                BuildStoveVisual();
                break;
            default:
                BuildSolidBlockVisual();
                break;
        }
    }

    private void BuildBillboardVisual()
    {
        _billboardSprite = new Sprite3D
        {
            PixelSize = 0.06f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Shaded = false,
        };

        Texture2D tex = TryLoadSprite();
        _hasAuthoredSprite = tex != null;
        _billboardSprite.Texture = tex ?? GeneratePlaceholderTexture();
        _billboardSprite.Modulate = Colors.White;
        _billboardRoot.AddChild(_billboardSprite);
        UpdateBillboardPresentation();
    }

    private void BuildSolidBlockVisual()
    {
        float px = Settings.BlockPixelSize;
        float height = Def.VisualHeight ?? 0.9f * px;
        AddBox(
            new Vector3(EffectiveSize.X * px * 0.7f, height, EffectiveSize.Y * px * 0.7f),
            new Vector3(0f, height * 0.5f, 0f),
            Def.GhostColor with { A = 1f });
    }

    private void BuildBedVisual()
    {
        float px = Settings.BlockPixelSize;
        Color frame = new Color(0.62f, 0.44f, 0.28f);
        Color blanket = new Color(0.76f, 0.78f, 0.93f);
        Color pillow = new Color(0.96f, 0.96f, 0.92f);

        AddBox(new Vector3(px * 0.82f, px * 0.18f, px * 1.65f), new Vector3(0f, px * 0.09f, 0f), frame);
        AddBox(new Vector3(px * 0.75f, px * 0.24f, px * 1.1f), new Vector3(0f, px * 0.22f, px * 0.14f), blanket);
        AddBox(new Vector3(px * 0.7f, px * 0.14f, px * 0.35f), new Vector3(0f, px * 0.2f, -px * 0.53f), pillow);
    }

    private void BuildTableVisual()
    {
        float px = Settings.BlockPixelSize;
        Color wood = new Color(0.58f, 0.41f, 0.28f);
        AddBox(new Vector3(px * 1.6f, px * 0.14f, px * 0.72f), new Vector3(0f, px * 0.5f, 0f), wood);

        float legOffsetX = px * 0.65f;
        float legOffsetZ = px * 0.22f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddBox(
                    new Vector3(px * 0.12f, px * 0.5f, px * 0.12f),
                    new Vector3(legOffsetX * sx, px * 0.25f, legOffsetZ * sz),
                    wood * 0.85f);
            }
        }
    }

    private void BuildWorkstationVisual()
    {
        float px = Settings.BlockPixelSize;
        Color wood = new Color(0.56f, 0.42f, 0.26f);
        Color accent = new Color(0.77f, 0.69f, 0.48f);
        AddBox(new Vector3(px * 1.6f, px * 0.18f, px * 0.72f), new Vector3(0f, px * 0.54f, 0f), wood);
        AddBox(new Vector3(px * 1.45f, px * 0.12f, px * 0.2f), new Vector3(0f, px * 0.72f, -px * 0.22f), accent);
        AddBox(new Vector3(px * 0.18f, px * 0.54f, px * 0.18f), new Vector3(-px * 0.62f, px * 0.27f, px * 0.18f), wood * 0.88f);
        AddBox(new Vector3(px * 0.18f, px * 0.54f, px * 0.18f), new Vector3(px * 0.62f, px * 0.27f, px * 0.18f), wood * 0.88f);
    }

    private void BuildStoveVisual()
    {
        float px = Settings.BlockPixelSize;
        Color stone = new Color(0.35f, 0.33f, 0.37f);
        Color trim = new Color(0.78f, 0.48f, 0.22f);

        AddBox(new Vector3(px * 1.2f, px * 0.85f, px * 0.75f), new Vector3(0f, px * 0.425f, 0f), stone);
        AddBox(new Vector3(px * 0.6f, px * 0.08f, px * 0.45f), new Vector3(0f, px * 0.62f, 0f), trim);

        var chimney = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = px * 0.1f,
                BottomRadius = px * 0.12f,
                Height = px * 0.6f,
                RadialSegments = 10,
            },
            Position = new Vector3(px * 0.32f, px * 1.02f, -px * 0.14f),
            MaterialOverride = CreateMaterial(stone * 0.92f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _meshRoot.AddChild(chimney);
        _occlusionParts.Add(chimney);
    }

    private void AddBox(Vector3 size, Vector3 offset, Color color)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = offset,
            MaterialOverride = CreateMaterial(color),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _meshRoot.AddChild(mesh);
        _occlusionParts.Add(mesh);
    }

    private StandardMaterial3D CreateMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.9f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    private void UpdateOcclusion()
    {
        if (_occlusionParts.Count == 0 || GameCamera.Instance == null || _meshRoot == null || !_meshRoot.Visible)
        {
            foreach (var part in _occlusionParts)
                part.Transparency = 0f;
            return;
        }

        if (GameCamera.Instance.ViewMode != CameraViewMode.Angled3D)
        {
            foreach (var part in _occlusionParts)
                part.Transparency = 0f;
            return;
        }

        Vector2 buildingScreen = GameCamera.Instance.UnprojectPosition(GlobalPosition);
        Vector2 mouseScreen = GameCamera.Instance.GetOcclusionMouseScreenPosition();
        float nearestDistance = buildingScreen.DistanceTo(mouseScreen);

        if (GameCamera.Instance.TryGetSelectedPawnScreenPosition(out Vector2 selectedPawnScreen))
            nearestDistance = Mathf.Min(nearestDistance, buildingScreen.DistanceTo(selectedPawnScreen));

        float transparency = nearestDistance <= GameCamera.Instance.OcclusionScreenRadiusPixels
            ? 1f - GameCamera.Instance.OcclusionAlpha
            : 0f;

        foreach (var part in _occlusionParts)
            part.Transparency = transparency;
    }

    private void UpdatePresentationMode()
    {
        bool angled3D = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;
        bool showMesh = angled3D && Def.View3DStyle != BuildingView3DStyle.Billboard;
        bool keepSprite = !showMesh || _hasAuthoredSprite;

        if (_billboardRoot != null)
        {
            _billboardRoot.Visible = keepSprite;
            _billboardRoot.Position = angled3D && showMesh
                ? new Vector3(0f, Settings.BlockPixelSize * 0.35f, 0f)
                : Vector3.Zero;
        }

        if (_meshRoot != null)
            _meshRoot.Visible = showMesh;

        if (_billboardSprite != null)
        {
            _billboardSprite.Modulate = angled3D && showMesh
                ? new Color(1f, 1f, 1f, 0.92f)
                : Colors.White;
        }

        UpdateBillboardPresentation();
    }

    private void UpdateBillboardPresentation()
    {
        if (_billboardSprite == null)
            return;

        bool angled3D = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;
        _billboardSprite.NoDepthTest = angled3D;
        _billboardSprite.RenderPriority = angled3D ? 7 : 0;

        if (_billboardSprite.Texture != null)
        {
            float halfHeight = _billboardSprite.Texture.GetHeight() * _billboardSprite.PixelSize * 0.5f;
            _billboardSprite.Position = new Vector3(0f, halfHeight, 0f);
        }
    }

    private Texture2D TryLoadSprite()
    {
        string path = Def.Id switch
        {
            "workbench" or "stove" or "research_desk" => "res://sprites/Sprout Lands/Objects/work station.png",
            _ => null,
        };

        if (path != null && ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);

        return null;
    }

    private ImageTexture GeneratePlaceholderTexture()
    {
        int size = 16;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        Color color = Def.GhostColor with { A = 1f };
        Color border = color * 0.7f;
        border.A = 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                image.SetPixel(x, y, isBorder ? border : color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static string CategoryName(string cat) => cat switch
    {
        "Structure" => "缁撴瀯",
        "Furniture" => "瀹跺叿",
        "Production" => "鐢熶骇",
        _ => cat,
    };
}
