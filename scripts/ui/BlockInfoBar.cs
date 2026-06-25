using EndfieldZero.World;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Small WTHIT-style info bar that shows the block under the mouse cursor.
/// </summary>
public partial class BlockInfoBar : PanelContainer
{
    private Label _titleLabel;
    private Label _detailLabel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(320f, 0f);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.1f, 0.88f),
            BorderColor = new Color(0.55f, 0.85f, 0.55f, 0.7f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8,
        };
        AddThemeStyleboxOverride("panel", panelStyle);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 2);
        AddChild(root);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 15);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 0.95f));
        root.AddChild(_titleLabel);

        _detailLabel = new Label();
        _detailLabel.AddThemeFontSizeOverride("font_size", 11);
        _detailLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.84f, 0.8f));
        _detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_detailLabel);

        Visible = false;
    }

    public override void _Process(double delta)
    {
        var viewport = GetViewport();
        var camera = viewport?.GetCamera3D();
        var world = WorldManager.Instance;
        if (viewport == null || camera == null || world == null)
        {
            Visible = false;
            return;
        }

        var hit = world.ScreenToBlockHit(viewport.GetMousePosition(), camera);
        var column = hit.Column;
        if (!hit.Hit || !column.HasSurface || column.Def == null || column.Block.IsAir)
        {
            Visible = false;
            return;
        }

        Visible = true;
        UpdateContent(column);
    }

    private void UpdateContent(SurfaceColumnInfo column)
    {
        BlockDef def = column.Def;
        string moveText = def.MoveSpeedMod <= 0f ? "Blocked" : $"{def.MoveSpeedMod:0.##}x";

        _titleLabel.Text = def.Name;
        _detailLabel.Text =
            $"X:{column.BlockCoord.X} Z:{column.BlockCoord.Y}  " +
            $"Layer:{column.Layer}  " +
            $"Solid:{YesNo(def.IsSolid)}  " +
            $"Transparent:{YesNo(def.IsTransparent)}  " +
            $"Move:{moveText}  " +
            $"Visual:{FormatVisualKind(def.VisualKind)}";
    }

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string FormatVisualKind(BlockVisualKind kind)
    {
        return kind switch
        {
            BlockVisualKind.FlatTop => "Flat",
            BlockVisualKind.SolidColumn => "Column",
            BlockVisualKind.Water => "Water",
            BlockVisualKind.Cross => "Cross",
            _ => kind.ToString(),
        };
    }
}
