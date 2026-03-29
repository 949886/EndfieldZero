using EndfieldZero.World;
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
    private Button _topDownButton;
    private Button _angledButton;
    private Label _hintLabel;

    public override void _Ready()
    {
        Alignment = AlignmentMode.Center;
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeConstantOverride("separation", 4);

        _buttons = new PanelContainer[Modes.Length];
        _labels = new Label[Modes.Length];

        for (int i = 0; i < Modes.Length; i++)
        {
            var modeInfo = Modes[i];
            var panel = new PanelContainer
            {
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.1f, 0.15f, 0.7f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                ContentMarginLeft = 8, ContentMarginRight = 8,
                ContentMarginTop = 4, ContentMarginBottom = 4,
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var label = new Label
            {
                Text = $"{modeInfo.Key}:{modeInfo.Label}",
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            label.MouseFilter = MouseFilterEnum.Ignore;

            panel.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mb &&
                    mb.ButtonIndex == MouseButton.Left &&
                    mb.Pressed)
                {
                    ToolModeManager.Instance?.SetMode(modeInfo.Mode);
                    AcceptEvent();
                }
            };

            panel.AddChild(label);
            AddChild(panel);
            _buttons[i] = panel;
            _labels[i] = label;
        }

        var separator = new VSeparator();
        separator.CustomMinimumSize = new Vector2(10f, 24f);
        AddChild(separator);

        _topDownButton = CreateViewButton("俯视", CameraViewMode.TopDown);
        _angledButton = CreateViewButton("3D", CameraViewMode.Angled3D);
        AddChild(_topDownButton);
        AddChild(_angledButton);

        _hintLabel = new Label
        {
            Text = "Tab 切换 | Alt+Q/E 旋转",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        _hintLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.72f));
        AddChild(_hintLabel);
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

        UpdateViewButtons();
    }

    private Button CreateViewButton(string text, CameraViewMode mode)
    {
        var button = new Button
        {
            Text = text,
            Flat = false,
            CustomMinimumSize = new Vector2(56f, 28f),
        };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += () => GameCamera.Instance?.SetViewMode(mode);
        return button;
    }

    private void UpdateViewButtons()
    {
        if (_topDownButton == null || _angledButton == null)
            return;

        ApplyViewButtonState(_topDownButton, GameCamera.Instance?.ViewMode == CameraViewMode.TopDown);
        ApplyViewButtonState(_angledButton, GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D);
    }

    private static void ApplyViewButtonState(Button button, bool active)
    {
        var style = new StyleBoxFlat
        {
            BgColor = active ? new Color(0.2f, 0.45f, 0.82f, 0.78f) : new Color(0.1f, 0.1f, 0.15f, 0.72f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = active ? new Color(0.62f, 0.82f, 1f, 0.95f) : new Color(0.28f, 0.28f, 0.36f, 0.65f),
        };
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeColorOverride("font_color", active ? Colors.White : new Color(0.85f, 0.85f, 0.9f));
    }
}
