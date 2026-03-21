using System.Collections.Generic;
using System.Linq;
using EndfieldZero.Building;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Bottom-of-screen building selection menu.
/// Shown when in Construct mode, displays categories and buildings.
///
///  [ 结构 ] [ 家具 ] [ 生产 ]
///      ↓
///  [ 石墙 ] [ 木墙 ] [ 门 ] [ 地板 ]
///
/// Selecting a building sets ToolModeManager.SelectedBuildingDef.
/// </summary>
public partial class BuildSubMenu : PanelContainer
{
    private HBoxContainer _categoryBar;
    private HBoxContainer _itemBar;
    private Label _descLabel;

    private string _activeCategory = "";

    // Style
    private static readonly Color PanelBg = new(0.08f, 0.08f, 0.12f, 0.88f);
    private static readonly Color ActiveBtnBg = new(0.25f, 0.5f, 0.7f, 0.6f);
    private static readonly Color InactiveBtnBg = new(0.12f, 0.12f, 0.18f, 0.7f);

    public override void _Ready()
    {
        // Ensure this panel can receive mouse events
        MouseFilter = MouseFilterEnum.Stop;
        // Main panel style
        var style = new StyleBoxFlat
        {
            BgColor = PanelBg,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            BorderWidthTop = 1, BorderColor = new Color(0.3f, 0.6f, 0.8f, 0.4f),
        };
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        AddChild(vbox);

        // Category bar
        _categoryBar = new HBoxContainer();
        _categoryBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_categoryBar);

        // Items bar
        _itemBar = new HBoxContainer();
        _itemBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_itemBar);

        // Description label
        _descLabel = new Label();
        _descLabel.AddThemeFontSizeOverride("font_size", 11);
        _descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
        vbox.AddChild(_descLabel);

        BuildCategories();
        Visible = false;
    }

    public override void _Process(double delta)
    {
        bool shouldShow = ToolModeManager.Instance?.CurrentMode == ToolMode.Construct;
        if (shouldShow != Visible)
            Visible = shouldShow;
    }

    private void BuildCategories()
    {
        // Clear
        foreach (var child in _categoryBar.GetChildren())
            child.QueueFree();

        var categories = BuildingRegistry.Instance.Categories.ToList();
        var categoryNames = new Dictionary<string, string>
        {
            { "Structure", "结构" },
            { "Furniture", "家具" },
            { "Production", "生产" },
        };

        foreach (var cat in categories)
        {
            string label = categoryNames.GetValueOrDefault(cat, cat);
            var btn = CreateButton(label, () => SelectCategory(cat));
            _categoryBar.AddChild(btn);
        }

        if (categories.Count > 0)
            SelectCategory(categories[0]);
    }

    private void SelectCategory(string category)
    {
        _activeCategory = category;

        // Clear items
        foreach (var child in _itemBar.GetChildren())
            child.QueueFree();

        var defs = BuildingRegistry.Instance.GetByCategory(category).ToList();

        foreach (var def in defs)
        {
            var btn = CreateButton(def.DisplayName, () => SelectBuilding(def));
            // Color the button with ghost color
            var btnStyle = btn.GetThemeStylebox("panel") as StyleBoxFlat;
            if (btnStyle != null)
                btnStyle.BorderColor = def.GhostColor with { A = 0.6f };
            _itemBar.AddChild(btn);
        }

        // Update category button highlights
        int i = 0;
        foreach (var catBtn in _categoryBar.GetChildren())
        {
            if (catBtn is PanelContainer pc)
            {
                var s = pc.GetThemeStylebox("panel") as StyleBoxFlat;
                var cats = BuildingRegistry.Instance.Categories.ToList();
                if (s != null && i < cats.Count)
                    s.BgColor = cats[i] == category ? ActiveBtnBg : InactiveBtnBg;
                i++;
            }
        }

        _descLabel.Text = "";
    }

    private void SelectBuilding(BuildingDef def)
    {
        if (ToolModeManager.Instance != null)
        {
            ToolModeManager.Instance.SelectedBuildingDef = def;
        }

        // Show description
        var matText = string.Join(", ", def.Materials.Select(m => $"{m.Key}×{m.Value}"));
        _descLabel.Text = $"{def.DisplayName} ({def.Size.X}×{def.Size.Y}) | 材料: {matText} | 工时: {def.WorkTicks}";
    }

    private PanelContainer CreateButton(string text, System.Action onClick)
    {
        var panel = new PanelContainer();
        var btnStyle = new StyleBoxFlat
        {
            BgColor = InactiveBtnBg,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 4, ContentMarginBottom = 4,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.4f, 0.5f, 0.4f),
        };
        panel.AddThemeStyleboxOverride("panel", btnStyle);

        var btn = new Button { Text = text, Flat = true };
        btn.AddThemeFontSizeOverride("font_size", 12);
        btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
        btn.Pressed += onClick;
        panel.AddChild(btn);

        return panel;
    }
}
