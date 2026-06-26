using System;
using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.UI;

public partial class SettingsOverlay : Control
{
    private const float ContentWidth = 760f;

    private enum MainSettingsTab
    {
        Graphics,
        Audio,
        Game,
    }

    private readonly record struct CycleOption<T>(string Label, T Value);

    private static readonly Color BackdropColor = new(0.36f, 0.28f, 0.2f, 0.35f);
    private static readonly Color PanelColor = new(0.96f, 0.94f, 0.88f, 0.98f);
    private static readonly Color AccentColor = new(0.74f, 0.78f, 0.06f, 1f);
    private static readonly Color AccentSoftColor = new(0.73f, 0.75f, 0.45f, 0.22f);
    private static readonly Color TextColor = new(0.33f, 0.23f, 0.21f, 1f);
    private static readonly Color MutedTextColor = new(0.56f, 0.48f, 0.44f, 1f);
    private static readonly Color DisabledTextColor = new(0.78f, 0.73f, 0.68f, 1f);
    private static readonly Color RowColor = new(1f, 1f, 1f, 0.18f);
    private static readonly Color RowBorderColor = new(0.76f, 0.7f, 0.62f, 0.65f);

    private static readonly CycleOption<DisplayModePreference>[] DisplayModes =
    {
        new("Windowed", DisplayModePreference.Windowed),
        new("Fullscreen", DisplayModePreference.Fullscreen),
        new("Borderless", DisplayModePreference.BorderlessFullscreen),
    };

    private static readonly CycleOption<Vector2I>[] ResolutionOptions =
    {
        new("1280 x 720", new Vector2I(1280, 720)),
        new("1600 x 900", new Vector2I(1600, 900)),
        new("1920 x 1080", new Vector2I(1920, 1080)),
        new("2560 x 1440", new Vector2I(2560, 1440)),
        new("3840 x 2160", new Vector2I(3840, 2160)),
    };

    private static readonly CycleOption<int>[] FpsOptions =
    {
        new("30", 30),
        new("60", 60),
        new("120", 120),
        new("Unlimited", 0),
    };

    private static readonly CycleOption<CameraViewMode>[] ViewModes =
    {
        new("Top", CameraViewMode.TopDown),
        new("Perspective", CameraViewMode.Perspective3D),
        new("Ortho", CameraViewMode.Orthographic3D),
    };

    public static SettingsOverlay Instance { get; private set; }
    public static bool IsOpen => Instance?.Visible == true;

    private MainSettingsTab _activeMainTab = MainSettingsTab.Graphics;
    private AdvancedSettingsTab _activeAdvancedTab = AdvancedSettingsTab.World;

    private PlayerPreferences _defaultPreferences;
    private PlayerPreferences _savedPreferences;
    private PlayerPreferences _draftPreferences;

    private VBoxContainer _rootStack;
    private HBoxContainer _mainTabBar;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private VBoxContainer _contentStack;
    private Button _applyButton;
    private float _resumeGameSpeed = 1f;
    private bool _hadResumeSpeed;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildShell();

        _defaultPreferences = SettingsBootstrap.RuntimeDefaults?.Clone() ?? UserSettingsStore.CaptureRuntimeDefaults();
        _savedPreferences = SettingsBootstrap.ActivePreferences?.Clone() ?? UserSettingsStore.Load(_defaultPreferences);
        _draftPreferences = _savedPreferences.Clone();

        Visible = false;
        RefreshView();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (Visible)
        {
            if (key.Keycode == Key.Escape)
                CancelAndClose();

            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode != Key.Escape)
            return;

        if (ToolModeManager.Instance?.CurrentMode != ToolMode.Select)
            return;

        if (HasActiveSelection())
            return;

        OpenOverlay();
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
            return;

        if (@event is InputEventMouseButton
            || @event is InputEventMouseMotion
            || @event is InputEventKey)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildShell()
    {
        var backdrop = new ColorRect
        {
            Color = BackdropColor,
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 760f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(PanelColor, 28, 22, 22));
        center.AddChild(panel);

        _rootStack = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 720f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        _rootStack.AddThemeConstantOverride("separation", 16);
        panel.AddChild(_rootStack);

        _titleLabel = new Label { Text = "Settings" };
        _titleLabel.AddThemeFontSizeOverride("font_size", 28);
        _titleLabel.AddThemeColorOverride("font_color", TextColor);
        _rootStack.AddChild(_titleLabel);

        _mainTabBar = new HBoxContainer();
        _mainTabBar.CustomMinimumSize = new Vector2(ContentWidth, 0f);
        _mainTabBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mainTabBar.AddThemeConstantOverride("separation", 12);
        _rootStack.AddChild(_mainTabBar);

        _subtitleLabel = new Label();
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 13);
        _subtitleLabel.AddThemeColorOverride("font_color", MutedTextColor);
        _rootStack.AddChild(_subtitleLabel);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(ContentWidth, 0f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _rootStack.AddChild(scroll);

        _contentStack = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(ContentWidth, 0f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        _contentStack.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_contentStack);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        _rootStack.AddChild(footer);

        _applyButton = CreateFooterButton("Apply", AccentColor, ApplyAndClose);
        footer.AddChild(_applyButton);
        footer.AddChild(CreateFooterButton("Cancel", new Color(0.7f, 0.63f, 0.56f), CancelAndClose));
        footer.AddChild(CreateFooterButton("Reset", new Color(0.78f, 0.75f, 0.67f), ResetDraftToDefaults));
    }

    private void OpenOverlay()
    {
        _draftPreferences = _savedPreferences.Clone();
        PauseTime();
        Visible = true;
        RefreshView();
    }

    private void CloseOverlay()
    {
        Visible = false;
        ResumeTime();
    }

    private void ApplyAndClose()
    {
        _savedPreferences = _draftPreferences.Clone();
        UserSettingsStore.Save(_savedPreferences);
        UserSettingsStore.Apply(_savedPreferences);
        SettingsBootstrap.UpdateActivePreferences(_savedPreferences);
        CloseOverlay();
    }

    private void CancelAndClose()
    {
        _draftPreferences = _savedPreferences.Clone();
        CloseOverlay();
    }

    private void ResetDraftToDefaults()
    {
        _draftPreferences = _defaultPreferences.Clone();
        RefreshView();
    }

    private void PauseTime()
    {
        if (TimeManager.Instance == null)
            return;

        _resumeGameSpeed = TimeManager.Instance.GameSpeed;
        _hadResumeSpeed = true;
        TimeManager.Instance.GameSpeed = 0f;
    }

    private void ResumeTime()
    {
        if (!_hadResumeSpeed || TimeManager.Instance == null)
            return;

        TimeManager.Instance.GameSpeed = _resumeGameSpeed;
        _hadResumeSpeed = false;
    }

    private void RefreshView()
    {
        RebuildMainTabs();
        RebuildContent();
        _applyButton.Disabled = _draftPreferences.ContentEquals(_savedPreferences);
        _subtitleLabel.Text = _activeMainTab switch
        {
            MainSettingsTab.Graphics => "Display, frame pacing, and default camera behavior.",
            MainSettingsTab.Audio => "Bus levels are applied when matching audio buses exist.",
            _ => "Gameplay preferences plus optional advanced runtime tuning.",
        };
    }

    private void RebuildMainTabs()
    {
        ClearChildren(_mainTabBar);
        _mainTabBar.AddChild(CreateTabButton("Graphics", _activeMainTab == MainSettingsTab.Graphics, () =>
        {
            _activeMainTab = MainSettingsTab.Graphics;
            RefreshView();
        }));
        _mainTabBar.AddChild(CreateTabButton("Audio", _activeMainTab == MainSettingsTab.Audio, () =>
        {
            _activeMainTab = MainSettingsTab.Audio;
            RefreshView();
        }));
        _mainTabBar.AddChild(CreateTabButton("Game", _activeMainTab == MainSettingsTab.Game, () =>
        {
            _activeMainTab = MainSettingsTab.Game;
            RefreshView();
        }));
    }

    private void RebuildContent()
    {
        ClearChildren(_contentStack);

        switch (_activeMainTab)
        {
            case MainSettingsTab.Graphics:
                BuildGraphicsPage();
                break;
            case MainSettingsTab.Audio:
                BuildAudioPage();
                break;
            case MainSettingsTab.Game:
                BuildGamePage();
                break;
        }
    }

    private void BuildGraphicsPage()
    {
        _contentStack.AddChild(CreateCycleRow(
            "Display Mode",
            DisplayModes,
            Array.FindIndex(DisplayModes, option => option.Value == _draftPreferences.DisplayMode),
            index =>
            {
                _draftPreferences.DisplayMode = DisplayModes[index].Value;
                RefreshView();
            }));

        bool resolutionEnabled = _draftPreferences.DisplayMode == DisplayModePreference.Windowed;
        _contentStack.AddChild(CreateCycleRow(
            "Resolution",
            EnsureResolutionOption(_draftPreferences.WindowSize),
            FindResolutionIndex(_draftPreferences.WindowSize),
            index =>
            {
                _draftPreferences.WindowSize = EnsureResolutionOption(_draftPreferences.WindowSize)[index].Value;
            },
            resolutionEnabled));

        _contentStack.AddChild(CreateToggleRow(
            "VSync",
            _draftPreferences.VSyncEnabled,
            value => _draftPreferences.VSyncEnabled = value));

        _contentStack.AddChild(CreateCycleRow(
            "FPS Cap",
            FpsOptions,
            Array.FindIndex(FpsOptions, option => option.Value == _draftPreferences.FpsLimit),
            index => _draftPreferences.FpsLimit = FpsOptions[index].Value));

        _contentStack.AddChild(CreateCycleRow(
            "Default View",
            ViewModes,
            Array.FindIndex(ViewModes, option => option.Value == _draftPreferences.DefaultViewMode),
            index => _draftPreferences.DefaultViewMode = ViewModes[index].Value));
    }

    private void BuildAudioPage()
    {
        _contentStack.AddChild(CreateSliderRow(
            "Master",
            _draftPreferences.MasterVolume,
            0f,
            1f,
            0.01f,
            value => _draftPreferences.MasterVolume = value,
            value => $"{Mathf.RoundToInt(value * 100f)}%"));

        _contentStack.AddChild(CreateSliderRow(
            "Music",
            _draftPreferences.MusicVolume,
            0f,
            1f,
            0.01f,
            value => _draftPreferences.MusicVolume = value,
            value => $"{Mathf.RoundToInt(value * 100f)}%"));

        _contentStack.AddChild(CreateSliderRow(
            "SFX",
            _draftPreferences.SfxVolume,
            0f,
            1f,
            0.01f,
            value => _draftPreferences.SfxVolume = value,
            value => $"{Mathf.RoundToInt(value * 100f)}%"));

        _contentStack.AddChild(CreateToggleRow(
            "Audio In Background",
            _draftPreferences.AudioInBackground,
            value => _draftPreferences.AudioInBackground = value));

        _contentStack.AddChild(CreateHintLabel("Music and SFX sliders are safe even before dedicated buses exist."));
    }

    private void BuildGamePage()
    {
        _contentStack.AddChild(CreateToggleRow(
            "Show Tutorial",
            _draftPreferences.ShowTutorial,
            value => _draftPreferences.ShowTutorial = value));

        _contentStack.AddChild(CreateToggleRow(
            "Show Debug HUD",
            _draftPreferences.ShowDebugHud,
            value => _draftPreferences.ShowDebugHud = value));

        _contentStack.AddChild(CreateToggleRow(
            "Advanced Mode",
            _draftPreferences.ShowAdvancedSettings,
            value =>
            {
                _draftPreferences.ShowAdvancedSettings = value;
                RefreshView();
            }));

        if (!_draftPreferences.ShowAdvancedSettings)
            return;

        var advancedTabs = new HBoxContainer();
        advancedTabs.CustomMinimumSize = new Vector2(ContentWidth, 0f);
        advancedTabs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        advancedTabs.AddThemeConstantOverride("separation", 8);
        _contentStack.AddChild(advancedTabs);

        AddAdvancedTabButton(advancedTabs, "World", AdvancedSettingsTab.World);
        AddAdvancedTabButton(advancedTabs, "Hostiles", AdvancedSettingsTab.Hostiles);
        AddAdvancedTabButton(advancedTabs, "Raids", AdvancedSettingsTab.Raids);
        AddAdvancedTabButton(advancedTabs, "Weapons", AdvancedSettingsTab.Weapons);
        AddAdvancedTabButton(advancedTabs, "Environment", AdvancedSettingsTab.Environment);

        foreach (var spec in SettingsFieldRegistry.AdvancedFields)
        {
            if (spec.Tab != _activeAdvancedTab)
                continue;

            _contentStack.AddChild(CreateAdvancedSliderRow(spec));
        }
    }

    private void AddAdvancedTabButton(HBoxContainer container, string label, AdvancedSettingsTab tab)
    {
        container.AddChild(CreateTabButton(label, _activeAdvancedTab == tab, () =>
        {
            _activeAdvancedTab = tab;
            RefreshView();
        }, compact: true));
    }

    private Control CreateAdvancedSliderRow(SettingFieldSpec spec)
    {
        double currentValue = spec.GetPreferenceValue(_draftPreferences);
        return CreateSliderRow(
            spec.Label,
            (float)currentValue,
            (float)spec.MinValue,
            (float)spec.MaxValue,
            (float)spec.Step,
            value => spec.SetPreferenceValue(_draftPreferences, value),
            _ => spec.FormatValue(_draftPreferences));
    }

    private Control CreateCycleRow<T>(
        string label,
        IReadOnlyList<CycleOption<T>> options,
        int selectedIndex,
        Action<int> onChanged,
        bool enabled = true)
    {
        int safeIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
        var editor = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 10);

        var prevButton = CreateArrowButton("<", enabled, () =>
        {
            int nextIndex = Mathf.PosMod(safeIndex - 1, options.Count);
            onChanged(nextIndex);
            if (enabled)
                RefreshView();
        });
        editor.AddChild(prevButton);

        var valueLabel = new Label
        {
            Text = options[safeIndex].Label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(220f, 42f),
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 26);
        valueLabel.AddThemeColorOverride("font_color", enabled ? AccentColor : DisabledTextColor);
        editor.AddChild(valueLabel);

        var nextButton = CreateArrowButton(">", enabled, () =>
        {
            int nextIndex = (safeIndex + 1) % options.Count;
            onChanged(nextIndex);
            if (enabled)
                RefreshView();
        });
        editor.AddChild(nextButton);

        return CreateRow(label, editor, enabled);
    }

    private Control CreateToggleRow(string label, bool currentValue, Action<bool> onChanged)
    {
        var button = new Button
        {
            ToggleMode = true,
            ButtonPressed = currentValue,
            Text = currentValue ? "On" : "Off",
            CustomMinimumSize = new Vector2(120f, 42f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        button.Toggled += pressed =>
        {
            button.Text = pressed ? "On" : "Off";
            onChanged(pressed);
            ApplyToggleStyle(button, pressed);
        };
        ApplyToggleStyle(button, currentValue);
        return CreateRow(label, button, true);
    }

    private Control CreateSliderRow(
        string label,
        float currentValue,
        float minValue,
        float maxValue,
        float step,
        Action<float> onChanged,
        Func<float, string> formatter)
    {
        var editor = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.End,
        };
        editor.AddThemeConstantOverride("separation", 12);

        var slider = new HSlider
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Step = step,
            Value = currentValue,
            CustomMinimumSize = new Vector2(260f, 32f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TicksOnBorders = true,
        };
        slider.AddThemeStyleboxOverride("slider", CreatePanelStyle(new Color(AccentColor, 0.32f), 10, 6, 6));
        slider.AddThemeStyleboxOverride("grabber_area", CreatePanelStyle(new Color(1f, 1f, 1f, 0.01f), 8, 0, 0));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", CreatePanelStyle(new Color(1f, 1f, 1f, 0.01f), 8, 0, 0));
        slider.AddThemeStyleboxOverride("grabber", CreatePanelStyle(new Color(0.67f, 0.59f, 0.52f), 10, 10, 10));
        editor.AddChild(slider);

        var valueLabel = new Label
        {
            Text = formatter(currentValue),
            CustomMinimumSize = new Vector2(92f, 0f),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 20);
        valueLabel.AddThemeColorOverride("font_color", AccentColor);
        editor.AddChild(valueLabel);

        slider.ValueChanged += value =>
        {
            onChanged((float)value);
            valueLabel.Text = formatter((float)value);
        };

        return CreateRow(label, editor, true);
    }

    private Control CreateRow(string label, Control editor, bool enabled)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(ContentWidth, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(RowColor, 18, 16, 14, RowBorderColor));

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);

        var nameLabel = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(260f, 0f),
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 23);
        nameLabel.AddThemeColorOverride("font_color", enabled ? TextColor : DisabledTextColor);
        row.AddChild(nameLabel);

        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editor.Modulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.55f);
        if (!enabled)
            editor.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(editor);

        return panel;
    }

    private Label CreateHintLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", MutedTextColor);
        return label;
    }

    private Button CreateTabButton(string label, bool active, Action onPressed, bool compact = false)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0f, compact ? 42f : 50f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        button.AddThemeFontSizeOverride("font_size", compact ? 18 : 20);
        button.Pressed += onPressed;

        var fill = active ? new Color(AccentColor, 0.26f) : new Color(0.72f, 0.67f, 0.6f, 0.16f);
        var hoverFill = active ? new Color(AccentColor, 0.32f) : new Color(0.72f, 0.67f, 0.6f, 0.24f);
        var pressedFill = active ? new Color(AccentColor, 0.36f) : new Color(0.72f, 0.67f, 0.6f, 0.3f);
        var textColor = active ? AccentColor : MutedTextColor;

        var normalStyle = CreatePanelStyle(fill, 18, 14, 10);
        var hoverStyle = CreatePanelStyle(hoverFill, 18, 14, 10);
        var pressedStyle = CreatePanelStyle(pressedFill, 18, 14, 10);
        if (active)
        {
            normalStyle.ShadowColor = new Color(AccentColor, 0.18f);
            normalStyle.ShadowSize = 4;
            hoverStyle.ShadowColor = new Color(AccentColor, 0.22f);
            hoverStyle.ShadowSize = 4;
            pressedStyle.ShadowColor = new Color(AccentColor, 0.24f);
            pressedStyle.ShadowSize = 4;
        }

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeColorOverride("font_color", textColor);
        return button;
    }

    private Button CreateArrowButton(string label, bool enabled, Action onPressed)
    {
        var button = new Button
        {
            Text = label,
            Disabled = !enabled,
            CustomMinimumSize = new Vector2(58f, 42f),
        };
        button.AddThemeFontSizeOverride("font_size", 22);
        button.Pressed += onPressed;

        var style = CreatePanelStyle(new Color(0.71f, 0.63f, 0.56f, enabled ? 0.95f : 0.35f), 16, 10, 8);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.65f));
        return button;
    }

    private Button CreateFooterButton(string label, Color fillColor, Action onPressed)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(170f, 52f),
        };
        button.AddThemeFontSizeOverride("font_size", 22);
        button.Pressed += onPressed;

        var style = CreatePanelStyle(fillColor, 18, 16, 12);
        var disabledStyle = CreatePanelStyle(new Color(fillColor, 0.45f), 18, 16, 12);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("disabled", disabledStyle);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
        return button;
    }

    private void ApplyToggleStyle(Button button, bool active)
    {
        var fill = active ? AccentColor : new Color(0.71f, 0.63f, 0.56f);
        var style = CreatePanelStyle(fill, 16, 12, 10);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeColorOverride("font_color", Colors.White);
    }

    private static StyleBoxFlat CreatePanelStyle(Color fillColor, int radius, float horizontalPadding, float verticalPadding, Color? borderColor = null)
    {
        return new StyleBoxFlat
        {
            BgColor = fillColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
            ContentMarginLeft = horizontalPadding,
            ContentMarginRight = horizontalPadding,
            ContentMarginTop = verticalPadding,
            ContentMarginBottom = verticalPadding,
            BorderWidthLeft = borderColor.HasValue ? 1 : 0,
            BorderWidthRight = borderColor.HasValue ? 1 : 0,
            BorderWidthTop = borderColor.HasValue ? 1 : 0,
            BorderWidthBottom = borderColor.HasValue ? 1 : 0,
            BorderColor = borderColor ?? Colors.Transparent,
        };
    }

    private static void ClearChildren(Node node)
    {
        foreach (Node child in node.GetChildren())
            child.QueueFree();
    }

    private static CycleOption<Vector2I>[] EnsureResolutionOption(Vector2I currentResolution)
    {
        int index = FindResolutionIndex(currentResolution);
        if (index >= 0)
            return ResolutionOptions;

        var merged = new List<CycleOption<Vector2I>>(ResolutionOptions)
        {
            new($"{currentResolution.X} x {currentResolution.Y}", currentResolution),
        };
        return merged.ToArray();
    }

    private static int FindResolutionIndex(Vector2I resolution)
    {
        for (int i = 0; i < ResolutionOptions.Length; i++)
        {
            if (ResolutionOptions[i].Value == resolution)
                return i;
        }

        return ResolutionOptions.Length;
    }

    private bool HasActiveSelection()
    {
        var selection = SelectionManager.Instance;
        return selection != null
            && (selection.Selected.Count > 0 || selection.SelectedEntity != null);
    }
}
