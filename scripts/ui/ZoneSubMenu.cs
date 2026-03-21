using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Zone type selector shown when in Zone mode.
///
///  [ 仓储区 ] [ 种植区 ] [ 居住区 ] [ 垃圾区 ]
///
/// Selecting a zone type sets ToolModeManager.SelectedZoneType.
/// </summary>
public partial class ZoneSubMenu : PanelContainer
{
    private readonly struct ZoneInfo
    {
        public readonly string Type;
        public readonly string Label;
        public readonly Color Color;

        public ZoneInfo(string type, string label, Color color)
        {
            Type = type; Label = label; Color = color;
        }
    }

    private static readonly ZoneInfo[] ZoneTypes =
    {
        new("Stockpile", "仓储区", new Color(0.8f, 0.6f, 0.2f)),
        new("Growing",   "种植区", new Color(0.3f, 0.85f, 0.3f)),
        new("Home",      "居住区", new Color(0.3f, 0.5f, 1f)),
        new("Dumping",   "垃圾区", new Color(0.6f, 0.4f, 0.2f)),
    };

    private Button[] _buttons;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.88f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 6, ContentMarginBottom = 6,
            BorderWidthTop = 1, BorderColor = new Color(0.8f, 0.6f, 0.2f, 0.4f),
        };
        AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        AddChild(hbox);

        _buttons = new Button[ZoneTypes.Length];

        for (int i = 0; i < ZoneTypes.Length; i++)
        {
            var info = ZoneTypes[i];
            var btn = new Button
            {
                Text = info.Label,
                CustomMinimumSize = new Vector2(70, 28),
            };
            btn.AddThemeFontSizeOverride("font_size", 12);

            // Style
            var btnStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.18f, 0.7f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                ContentMarginLeft = 6, ContentMarginRight = 6,
                ContentMarginTop = 3, ContentMarginBottom = 3,
                BorderWidthLeft = 1, BorderWidthRight = 1,
                BorderWidthTop = 1, BorderWidthBottom = 1,
                BorderColor = info.Color with { A = 0.5f },
            };
            btn.AddThemeStyleboxOverride("normal", btnStyle);

            var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
            hoverStyle.BgColor = info.Color with { A = 0.3f };
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = (StyleBoxFlat)btnStyle.Duplicate();
            pressedStyle.BgColor = info.Color with { A = 0.5f };
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);

            string zoneType = info.Type;
            btn.Pressed += () => SelectZoneType(zoneType);
            hbox.AddChild(btn);
            _buttons[i] = btn;
        }

        Visible = false;
    }

    public override void _Process(double delta)
    {
        bool shouldShow = ToolModeManager.Instance?.CurrentMode == ToolMode.Zone;
        if (shouldShow != Visible)
            Visible = shouldShow;

        // Highlight selected zone type
        if (Visible && ToolModeManager.Instance != null)
        {
            string sel = ToolModeManager.Instance.SelectedZoneType;
            for (int i = 0; i < ZoneTypes.Length; i++)
            {
                bool active = ZoneTypes[i].Type == sel;
                _buttons[i].AddThemeColorOverride("font_color",
                    active ? Colors.White : new Color(0.7f, 0.7f, 0.75f));
            }
        }
    }

    private void SelectZoneType(string zoneType)
    {
        if (ToolModeManager.Instance != null)
            ToolModeManager.Instance.SelectedZoneType = zoneType;
    }
}
