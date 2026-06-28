using System;
using System.Collections.Generic;
using System.Linq;
using Cherry.Building;
using Cherry.Research;
using Godot;

namespace Cherry.UI;

/// <summary>
/// Bottom-of-screen building selection menu.
/// Shown when in Construct mode, displays categories and buildings.
/// Selecting a building sets ToolModeManager.SelectedBuildingDef.
/// </summary>
public partial class BuildSubMenu : PanelContainer
{
    private Button _brushModeButton;
    private Button _boxModeButton;
    private HBoxContainer _categoryBar;
    private HBoxContainer _itemBar;
    private Label _descLabel;

    private string _activeCategory = "";

    private static readonly Color PanelBg = new(0.08f, 0.08f, 0.12f, 0.88f);
    private static readonly Color ActiveBtnBg = new(0.25f, 0.5f, 0.7f, 0.6f);
    private static readonly Color InactiveBtnBg = new(0.12f, 0.12f, 0.18f, 0.7f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = PanelBg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            BorderWidthTop = 1,
            BorderColor = new Color(0.3f, 0.6f, 0.8f, 0.4f),
        };
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        AddChild(vbox);

        var modeBar = new HBoxContainer();
        modeBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(modeBar);

        _brushModeButton = CreateModeButton("Brush", ToolModeManager.ConstructPlacementMode.Brush);
        _boxModeButton = CreateModeButton("Box", ToolModeManager.ConstructPlacementMode.Box);
        modeBar.AddChild(_brushModeButton);
        modeBar.AddChild(_boxModeButton);

        _categoryBar = new HBoxContainer();
        _categoryBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_categoryBar);

        _itemBar = new HBoxContainer();
        _itemBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_itemBar);

        _descLabel = new Label();
        _descLabel.AddThemeFontSizeOverride("font_size", 11);
        _descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
        vbox.AddChild(_descLabel);

        if (TechnologyManager.Instance != null)
            TechnologyManager.Instance.Changed += OnTechnologyChanged;

        BuildCategories();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (TechnologyManager.Instance != null)
            TechnologyManager.Instance.Changed -= OnTechnologyChanged;
    }

    public override void _Process(double delta)
    {
        bool shouldShow = ToolModeManager.Instance?.CurrentMode == ToolMode.Construct;
        if (shouldShow != Visible)
            Visible = shouldShow;

        UpdatePlacementModeButtons();
    }

    private void BuildCategories()
    {
        foreach (Node child in _categoryBar.GetChildren())
            child.QueueFree();
        foreach (Node child in _itemBar.GetChildren())
            child.QueueFree();

        var categories = GetUnlockedCategories();
        Dictionary<string, string> categoryNames = new()
        {
            { "Structure", "Structure" },
            { "Furniture", "Furniture" },
            { "Production", "Production" },
        };

        if (categories.Count == 0)
        {
            _activeCategory = "";
            _descLabel.Text = "No unlocked buildings";
            return;
        }

        foreach (string category in categories)
        {
            string label = categoryNames.GetValueOrDefault(category, category);
            _categoryBar.AddChild(CreateButton(label, () => SelectCategory(category)));
        }

        if (!categories.Contains(_activeCategory))
            _activeCategory = categories[0];

        SelectCategory(_activeCategory);
    }

    private void SelectCategory(string category)
    {
        _activeCategory = category;

        foreach (Node child in _itemBar.GetChildren())
            child.QueueFree();

        var defs = BuildingRegistry.Instance
            .GetByCategory(category)
            .Where(def => TechnologyManager.Instance?.IsBuildingUnlocked(def.Id) ?? true)
            .ToList();

        foreach (BuildingDef def in defs)
        {
            var btn = CreateButton(def.DisplayName, () => SelectBuilding(def));
            if (btn.GetThemeStylebox("panel") is StyleBoxFlat btnStyle)
                btnStyle.BorderColor = def.GhostColor with { A = 0.6f };
            _itemBar.AddChild(btn);
        }

        int i = 0;
        var categories = GetUnlockedCategories();
        foreach (Node child in _categoryBar.GetChildren())
        {
            if (child is PanelContainer panel &&
                panel.GetThemeStylebox("panel") is StyleBoxFlat style &&
                i < categories.Count)
            {
                style.BgColor = categories[i] == category ? ActiveBtnBg : InactiveBtnBg;
                i++;
            }
        }

        _descLabel.Text = defs.Count == 0 ? "No unlocked buildings in this category" : "";
    }

    private void SelectBuilding(BuildingDef def)
    {
        if (TechnologyManager.Instance != null && !TechnologyManager.Instance.IsBuildingUnlocked(def.Id))
            return;

        if (ToolModeManager.Instance != null)
            ToolModeManager.Instance.SelectedBuildingDef = def;

        string matText = def.Materials.Count == 0
            ? "None"
            : string.Join(", ", def.Materials.Select(material => $"{material.Key} x{material.Value}"));
        _descLabel.Text = $"{def.DisplayName} ({def.Size.X}x{def.Size.Y}) | Cost: {matText} | Work: {def.WorkTicks}";
    }

    private Button CreateModeButton(string text, ToolModeManager.ConstructPlacementMode mode)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(72, 28),
        };
        btn.AddThemeFontSizeOverride("font_size", 12);

        var normalStyle = new StyleBoxFlat
        {
            BgColor = InactiveBtnBg,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.4f, 0.5f, 0.5f),
        };
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.18f, 0.18f, 0.24f, 0.85f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.Pressed += () =>
        {
            if (ToolModeManager.Instance != null)
                ToolModeManager.Instance.PlacementMode = mode;
            UpdatePlacementModeButtons();
        };

        return btn;
    }

    private void UpdatePlacementModeButtons()
    {
        if (_brushModeButton == null || _boxModeButton == null || ToolModeManager.Instance == null)
            return;

        bool brushActive = ToolModeManager.Instance.PlacementMode == ToolModeManager.ConstructPlacementMode.Brush;
        ApplyModeButtonState(_brushModeButton, brushActive);
        ApplyModeButtonState(_boxModeButton, !brushActive);
    }

    private void ApplyModeButtonState(Button button, bool active)
    {
        if (button.GetThemeStylebox("normal") is StyleBoxFlat style)
        {
            style.BgColor = active ? ActiveBtnBg : InactiveBtnBg;
            style.BorderColor = active
                ? new Color(0.45f, 0.75f, 0.95f, 0.85f)
                : new Color(0.3f, 0.4f, 0.5f, 0.5f);
        }

        button.AddThemeColorOverride("font_color",
            active ? Colors.White : new Color(0.8f, 0.8f, 0.86f));
    }

    private PanelContainer CreateButton(string text, Action onClick)
    {
        var panel = new PanelContainer();
        var btnStyle = new StyleBoxFlat
        {
            BgColor = InactiveBtnBg,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.4f, 0.5f, 0.4f),
        };
        panel.AddThemeStyleboxOverride("panel", btnStyle);

        var button = new Button { Text = text, Flat = true };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
        button.Pressed += onClick;
        panel.AddChild(button);

        return panel;
    }

    private List<string> GetUnlockedCategories()
    {
        return BuildingRegistry.Instance.Categories
            .Where(category => BuildingRegistry.Instance
                .GetByCategory(category)
                .Any(def => TechnologyManager.Instance?.IsBuildingUnlocked(def.Id) ?? true))
            .ToList();
    }

    private void OnTechnologyChanged()
    {
        BuildCategories();

        if (ToolModeManager.Instance?.SelectedBuildingDef != null &&
            TechnologyManager.Instance != null &&
            !TechnologyManager.Instance.IsBuildingUnlocked(ToolModeManager.Instance.SelectedBuildingDef.Id))
        {
            ToolModeManager.Instance.SelectedBuildingDef = null;
        }
    }
}
