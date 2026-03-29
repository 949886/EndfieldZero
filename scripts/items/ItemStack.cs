using EndfieldZero.Core;
using EndfieldZero.Farming;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Items;

/// <summary>
/// Runtime item instance on the ground or in a stockpile.
/// Rendered as a small billboard Sprite3D.
/// Implements ISelectable for click-to-inspect.
///
/// States:
///   OnGround    — dropped, waiting to be hauled
///   Reserved    — a pawn is coming to pick this up
///   BeingCarried — attached to a pawn (invisible on ground)
///   InStockpile — stored in a stockpile zone
/// </summary>
public partial class ItemStack : Node3D, ISelectable
{
    private static int _nextId = 1;

    public int ItemId { get; private set; }
    public ItemDef Def { get; private set; }
    public Vector2I BlockCoord { get; private set; }
    public int Count { get; set; }
    public ItemState State { get; set; } = ItemState.OnGround;

    // --- Selection ---
    public bool IsSelected { get; set; }
    public string SelectionTitle => $"{Def.DisplayName} ×{Count}";
    public string SelectionInfo
    {
        get
        {
            string stateStr = State switch
            {
                ItemState.OnGround => "📍 散落在地面",
                ItemState.Reserved => "🚶 等待搬运",
                ItemState.BeingCarried => "🎒 正在搬运中",
                ItemState.InStockpile => "📦 已入库",
                _ => "未知",
            };
            string info = $"{stateStr}\n📦 类别: {CategoryName(Def.Category)}";
            info += $"\n💰 价值: {Def.BaseValue * Count:F0}";
            if (Def.NutritionValue > 0)
                info += $"\n🍞 营养: {Def.NutritionValue:F0}/个";
            return info;
        }
    }

    // --- Rendering ---
    private Sprite3D _sprite;
    private SelectionCircleNode _selectionCircle;
    private Label3D _countLabel;

    public void Init(ItemDef def, Vector2I blockCoord, int count)
    {
        ItemId = _nextId++;
        Def = def;
        BlockCoord = blockCoord;
        Count = count;

        float px = Settings.BlockPixelSize;
        float baseY = WorldManager.Instance?.GetSurfaceTopY(blockCoord.X, blockCoord.Y) ?? 0f;
        Position = new Vector3(
            (blockCoord.X + 0.5f) * px,
            baseY + 0.05f,
            (blockCoord.Y + 0.5f) * px
        );
    }

    /// <summary>Move this item to a new block coordinate.</summary>
    public void MoveTo(Vector2I newCoord)
    {
        BlockCoord = newCoord;
        float px = Settings.BlockPixelSize;
        float baseY = WorldManager.Instance?.GetSurfaceTopY(newCoord.X, newCoord.Y) ?? 0f;
        Position = new Vector3(
            (newCoord.X + 0.5f) * px,
            baseY + 0.05f,
            (newCoord.Y + 0.5f) * px
        );
    }

    public override void _Ready()
    {
        // Create sprite
        _sprite = new Sprite3D
        {
            PixelSize = 0.04f,  // Smaller than pawns/crops
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Shaded = false,
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
        };

        if (Def.HasSprite)
        {
            var sheet = GD.Load<Texture2D>("res://sprites/Sprout Lands/Objects/Items/Farming Plants items.png");
            _sprite.Texture = sheet;
            _sprite.RegionEnabled = true;
            _sprite.RegionRect = new Rect2(
                Def.SpriteCol * ItemDef.TileSize,
                Def.SpriteRow * ItemDef.TileSize,
                ItemDef.TileSize, ItemDef.TileSize);
        }
        else
        {
            _sprite.Texture = GenerateIcon();
        }

        AddChild(_sprite);

        // Count label
        _countLabel = new Label3D
        {
            Text = Count > 1 ? $"×{Count}" : "",
            PixelSize = 0.008f,
            FontSize = 24,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0.15f, 0.15f, 0f),
            Modulate = new Color(1f, 1f, 1f, 0.9f),
            OutlineSize = 4,
            OutlineModulate = new Color(0f, 0f, 0f, 0.8f),
            NoDepthTest = true,
        };
        AddChild(_countLabel);

        // Selection circle
        _selectionCircle = new SelectionCircleNode();
        AddChild(_selectionCircle);
        _selectionCircle.Visible = false;
        UpdatePresentationMode();
    }

    public override void _Process(double delta)
    {
        _selectionCircle.Visible = IsSelected;
        Visible = State != ItemState.BeingCarried;
        UpdatePresentationMode();
        if (_countLabel != null)
            _countLabel.Text = Count > 1 ? $"×{Count}" : "";
    }

    private void UpdatePresentationMode()
    {
        bool angled3D = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;

        if (_sprite != null)
        {
            _sprite.NoDepthTest = angled3D;
            _sprite.RenderPriority = angled3D ? 9 : 0;
            _sprite.AlphaCut = angled3D
                ? SpriteBase3D.AlphaCutMode.Disabled
                : SpriteBase3D.AlphaCutMode.OpaquePrepass;
            UpdateSpriteAnchor();
        }

        if (_countLabel != null)
        {
            _countLabel.RenderPriority = angled3D ? 10 : 0;
            _countLabel.Position = new Vector3(0.15f, GetSpriteTopY() + 0.12f, 0f);
        }
    }

    private void UpdateSpriteAnchor()
    {
        if (_sprite == null)
            return;

        float halfHeight = GetSpriteHalfHeight();
        _sprite.Position = new Vector3(0f, halfHeight, 0f);
    }

    private float GetSpriteHalfHeight()
    {
        if (_sprite == null)
            return 0f;

        if (_sprite.RegionEnabled)
            return ItemDef.TileSize * _sprite.PixelSize * 0.5f;

        return (_sprite.Texture?.GetHeight() ?? 0) * _sprite.PixelSize * 0.5f;
    }

    private float GetSpriteTopY()
    {
        return GetSpriteHalfHeight() * 2f;
    }

    private ImageTexture GenerateIcon()
    {
        int size = 12;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var c = Def.IconColor;
        var border = (c * 0.6f); border.A = 1f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                // Round corners
                bool isCorner = (x < 2 && y < 2) || (x >= size - 2 && y < 2) ||
                                (x < 2 && y >= size - 2) || (x >= size - 2 && y >= size - 2);
                if (isCorner) image.SetPixel(x, y, Colors.Transparent);
                else image.SetPixel(x, y, isBorder ? border : c);
            }

        return ImageTexture.CreateFromImage(image);
    }

    private static string CategoryName(string cat) => cat switch
    {
        "Resource" => "资源",
        "Food" => "食物",
        "Material" => "材料",
        _ => cat,
    };
}

public enum ItemState
{
    OnGround,
    Reserved,
    BeingCarried,
    InStockpile,
}
