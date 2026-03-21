using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Small HUD bar showing the current tool mode with icon indicators.
/// Rendered at top-center of the screen.
///
///  [ Q:选择 ] [ M:挖矿 ] [ B:建造 ] [ G:种植 ] [ Z:区划 ] [ X:取消 ]
///
/// The active mode is highlighted with a colored background.
/// </summary>
public partial class ToolModeBar : HBoxContainer
{
    private readonly struct ModeInfo
    {
        public readonly ToolMode Mode;
        public readonly string Key;
        public readonly string Label;
        public readonly Color Color;

        public ModeInfo(ToolMode mode, string key, string label, Color color)
        {
            Mode = mode; Key = key; Label = label; Color = color;
        }
    }

    private static readonly ModeInfo[] Modes =
    {
        new(ToolMode.Select,    "Q", "选择", new Color(0.3f, 0.8f, 0.4f)),
        new(ToolMode.Mine,      "M", "挖矿", new Color(1f, 0.35f, 0.25f)),
        new(ToolMode.Construct, "B", "建造", new Color(0.35f, 0.55f, 1f)),
        new(ToolMode.Grow,      "G", "种植", new Color(0.35f, 0.9f, 0.35f)),
        new(ToolMode.Zone,      "Z", "区划", new Color(0.8f, 0.6f, 0.25f)),
        new(ToolMode.Cancel,    "X", "取消", new Color(1f, 0.8f, 0.25f)),
    };

    private PanelContainer[] _buttons;
    private Label[] _labels;

    public override void _Ready()
    {
        Alignment = AlignmentMode.Center;
        AddThemeConstantOverride("separation", 4);

        _buttons = new PanelContainer[Modes.Length];
        _labels = new Label[Modes.Length];

        for (int i = 0; i < Modes.Length; i++)
        {
            var panel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.1f, 0.15f, 0.7f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                ContentMarginLeft = 8, ContentMarginRight = 8,
                ContentMarginTop = 4, ContentMarginBottom = 4,
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var label = new Label { Text = $"{Modes[i].Key}:{Modes[i].Label}" };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            panel.AddChild(label);

            AddChild(panel);
            _buttons[i] = panel;
            _labels[i] = label;
        }
    }

    public override void _Process(double delta)
    {
        var currentMode = ToolModeManager.Instance?.CurrentMode ?? ToolMode.Select;

        for (int i = 0; i < Modes.Length; i++)
        {
            bool active = Modes[i].Mode == currentMode;

            var style = _buttons[i].GetThemeStylebox("panel") as StyleBoxFlat;
            if (style != null)
            {
                style.BgColor = active
                    ? Modes[i].Color with { A = 0.4f }
                    : new Color(0.1f, 0.1f, 0.15f, 0.7f);

                int bw = active ? 2 : 0;
                style.BorderWidthBottom = bw;
                style.BorderWidthTop = bw;
                style.BorderWidthLeft = bw;
                style.BorderWidthRight = bw;
                style.BorderColor = active ? Modes[i].Color with { A = 0.8f } : Colors.Transparent;
            }

            _labels[i].AddThemeColorOverride("font_color",
                active ? Colors.White : new Color(0.6f, 0.6f, 0.65f));
        }
    }
}
