using EndfieldZero.Core;
using EndfieldZero.Farming;
using Godot;

namespace EndfieldZero.Building;

/// <summary>
/// Runtime building entity — a completed (non-block) building in the world.
/// Used for furniture and production buildings that don't exist as blocks.
///
/// Rendered as a Sprite3D (billboard) with a colored rectangle.
/// Implements ISelectable for click-to-inspect.
/// </summary>
public partial class BuildingInstance : Node3D, ISelectable
{
    public BuildingDef Def { get; private set; }
    public Vector2I BlockCoord { get; private set; }
    public int BuildRotation { get; private set; }
    public Vector2I EffectiveSize => BuildRotation % 2 == 1
        ? new Vector2I(Def.Size.Y, Def.Size.X)
        : Def.Size;

    // --- Selection ---
    public bool IsSelected { get; set; }
    public string SelectionTitle => Def.DisplayName;
    public string SelectionInfo
    {
        get
        {
            string info = $"📦 类别: {CategoryName(Def.Category)}";
            info += $"\n📐 尺寸: {Def.Size.X}×{Def.Size.Y}";

            if (Def.SatisfiesNeed != null)
                info += $"\n💤 满足需求: {Def.SatisfiesNeed}";
            if (Def.ComfortOffset > 0)
                info += $"\n🛋️ 舒适: +{Def.ComfortOffset:F0}";
            if (Def.BeautyOffset > 0)
                info += $"\n🌸 美感: +{Def.BeautyOffset:F0}";

            return info;
        }
    }

    private Sprite3D _sprite;
    private SelectionCircleNode _selectionCircle;

    /// <summary>Initialize this building with its definition and position.</summary>
    public void Init(BuildingDef def, Vector2I blockCoord, int rotation = 0)
    {
        Def = def;
        BlockCoord = blockCoord;
        BuildRotation = rotation;

        float px = Settings.BlockPixelSize;
        var effSize = EffectiveSize;

        Position = new Vector3(
            (blockCoord.X + effSize.X * 0.5f) * px,
            0.02f,
            (blockCoord.Y + effSize.Y * 0.5f) * px
        );
    }

    public System.Collections.Generic.IEnumerable<Vector2I> OccupiedCells()
    {
        var size = EffectiveSize;
        for (int dz = 0; dz < size.Y; dz++)
            for (int dx = 0; dx < size.X; dx++)
                yield return new Vector2I(BlockCoord.X + dx, BlockCoord.Y + dz);
    }

    public override void _Ready()
    {
        // Create colored placeholder sprite
        _sprite = new Sprite3D
        {
            PixelSize = 0.06f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Shaded = false,
        };

        // Try loading a matching sprite, fallback to generated texture
        var tex = TryLoadSprite();
        _sprite.Texture = tex ?? GeneratePlaceholderTexture();
        AddChild(_sprite);

        // Selection circle
        _selectionCircle = new SelectionCircleNode();
        AddChild(_selectionCircle);
        _selectionCircle.Visible = false;
    }

    public override void _Process(double delta)
    {
        _selectionCircle.Visible = IsSelected;
    }

    private Texture2D TryLoadSprite()
    {
        // Try category-specific sprite sheets
        string path = Def.Id switch
        {
            "workbench" or "stove" or "research_desk" =>
                "res://sprites/Sprout Lands/Objects/work station.png",
            _ => null
        };

        if (path != null && ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);

        return null;
    }

    private ImageTexture GeneratePlaceholderTexture()
    {
        // Generate small colored icon based on building type
        int size = 16;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        var color = Def.GhostColor with { A = 1f };
        var border = color * 0.7f;
        border.A = 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                image.SetPixel(x, y, isBorder ? border : color);
            }
        }

        // Add icon pattern based on type
        switch (Def.Category)
        {
            case "Furniture":
                // Draw a small symbol
                for (int i = 4; i < 12; i++)
                {
                    image.SetPixel(i, 7, Colors.White);
                    image.SetPixel(i, 8, Colors.White);
                }
                break;
            case "Production":
                // Cross pattern
                for (int i = 3; i < 13; i++)
                {
                    image.SetPixel(8, i, Colors.White);
                    image.SetPixel(i, 8, Colors.White);
                }
                break;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static string CategoryName(string cat) => cat switch
    {
        "Structure" => "结构",
        "Furniture" => "家具",
        "Production" => "生产",
        _ => cat,
    };
}
