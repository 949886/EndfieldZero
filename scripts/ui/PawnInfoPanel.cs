using EndfieldZero.Farming;
using EndfieldZero.Managers;
using EndfieldZero.Pawn;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Bottom-left panel showing detailed info about the first selected pawn.
/// Displays: name, mood, needs bars, traits, AI action, stats.
///
/// Layout:
///   ┌─────────────────────────────────┐
///   │  Name (age, gender)       Mood  │
///   │  ───────────────────────────     │
///   │  AI: [current action]           │
///   │  ████████░░  Hunger  72/100     │
///   │  ██████████  Rest    95/100     │
///   │  ███████░░░  Joy     68/100     │
///   │  ████████░░  Comfort 80/100     │
///   │  ████████░░  Social  75/100     │
///   │  ───────────────────────────     │
///   │  [Trait1] [Trait2]              │
///   │  Mining:8  Construction:5       │
///   └─────────────────────────────────┘
/// </summary>
public partial class PawnInfoPanel : PanelContainer
{
    // Cached internal controls (created in code)
    private Label _nameLabel;
    private Label _moodLabel;
    private Label _aiLabel;
    private VBoxContainer _needsBars;
    private Label _traitsLabel;
    private Label _statsLabel;

    // Needs bar sub-items
    private ProgressBar[] _needProgressBars;
    private Label[] _needLabels;

    private static readonly string[] NeedNames = { "Hunger", "Rest", "Joy", "Comfort", "Beauty", "Social" };
    private static readonly string[] NeedDisplayNames = { "饥饿", "精力", "娱乐", "舒适", "美感", "社交" };
    private static readonly Color[] NeedColors =
    {
        new(0.9f, 0.5f, 0.2f),  // Hunger — orange
        new(0.3f, 0.6f, 1.0f),  // Rest — blue
        new(1.0f, 0.85f, 0.2f), // Joy — yellow
        new(0.6f, 0.4f, 0.8f),  // Comfort — purple
        new(0.9f, 0.5f, 0.7f),  // Beauty — pink
        new(0.3f, 0.9f, 0.6f),  // Social — teal
    };

    private Pawn.Pawn _lastPawn;

    public override void _Ready()
    {
        // Panel styling
        CustomMinimumSize = new Vector2(320, 0);

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.85f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.8f, 0.4f, 0.5f),
        };
        AddThemeStyleboxOverride("panel", styleBox);

        // Build UI tree
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        AddChild(vbox);

        // Header row: Name + Mood
        var headerRow = new HBoxContainer();
        vbox.AddChild(headerRow);

        _nameLabel = new Label();
        _nameLabel.AddThemeFontSizeOverride("font_size", 16);
        _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        headerRow.AddChild(_nameLabel);

        _moodLabel = new Label();
        _moodLabel.AddThemeFontSizeOverride("font_size", 14);
        _moodLabel.HorizontalAlignment = HorizontalAlignment.Right;
        headerRow.AddChild(_moodLabel);

        // Separator
        vbox.AddChild(CreateSeparator());

        // AI action
        _aiLabel = new Label();
        _aiLabel.AddThemeFontSizeOverride("font_size", 12);
        _aiLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
        vbox.AddChild(_aiLabel);

        // Needs bars
        _needsBars = new VBoxContainer();
        _needsBars.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_needsBars);

        _needProgressBars = new ProgressBar[NeedNames.Length];
        _needLabels = new Label[NeedNames.Length];

        for (int i = 0; i < NeedNames.Length; i++)
        {
            var row = new HBoxContainer();
            _needsBars.AddChild(row);

            var nameLabel = new Label { Text = NeedDisplayNames[i], CustomMinimumSize = new Vector2(40, 0) };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
            row.AddChild(nameLabel);

            var bar = new ProgressBar
            {
                MinValue = 0, MaxValue = 100,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(140, 14),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            // Style the bar
            var barBg = new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.15f, 0.2f),
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            };
            var barFill = new StyleBoxFlat
            {
                BgColor = NeedColors[i],
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            };
            bar.AddThemeStyleboxOverride("background", barBg);
            bar.AddThemeStyleboxOverride("fill", barFill);
            row.AddChild(bar);
            _needProgressBars[i] = bar;

            var valLabel = new Label { CustomMinimumSize = new Vector2(50, 0) };
            valLabel.AddThemeFontSizeOverride("font_size", 11);
            valLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
            valLabel.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(valLabel);
            _needLabels[i] = valLabel;
        }

        // Separator
        vbox.AddChild(CreateSeparator());

        // Traits
        _traitsLabel = new Label();
        _traitsLabel.AddThemeFontSizeOverride("font_size", 11);
        _traitsLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 1f));
        _traitsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(_traitsLabel);

        // Stats
        _statsLabel = new Label();
        _statsLabel.AddThemeFontSizeOverride("font_size", 11);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.75f));
        _statsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(_statsLabel);

        Visible = false;
    }

    public override void _Process(double delta)
    {
        var sel = SelectionManager.Instance;
        if (sel == null)
        {
            if (Visible) Visible = false;
            return;
        }

        // Priority: selected pawn > selected entity
        if (sel.Selected.Count > 0)
        {
            var pawn = sel.Selected[0];
            if (!pawn.IsAlive)
            {
                Visible = false;
                return;
            }
            Visible = true;
            ShowPawnLayout(true);
            UpdateInfo(pawn);
        }
        else if (sel.SelectedEntity != null)
        {
            Visible = true;
            ShowPawnLayout(false);
            UpdateEntityInfo(sel.SelectedEntity);
        }
        else
        {
            if (Visible) Visible = false;
            _lastPawn = null;
        }
    }

    /// <summary>Toggle visibility of pawn-specific UI elements.</summary>
    private void ShowPawnLayout(bool isPawn)
    {
        _aiLabel.Visible = isPawn;
        _needsBars.Visible = isPawn;
        _traitsLabel.Visible = isPawn;
        _statsLabel.Visible = isPawn;
    }

    /// <summary>Display info for a non-pawn ISelectable entity.</summary>
    private void UpdateEntityInfo(ISelectable entity)
    {
        _nameLabel.Text = entity.SelectionTitle;
        _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));

        _moodLabel.Text = "";

        // Use aiLabel to show detailed info (repurposed)
        _aiLabel.Visible = true;
        _aiLabel.Text = entity.SelectionInfo;
        _aiLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void UpdateInfo(Pawn.Pawn pawn)
    {
        // Name + identity
        string genderStr = pawn.Data.Gender switch
        {
            Gender.Male => "♂",
            Gender.Female => "♀",
            _ => "⚥",
        };
        _nameLabel.Text = $"{pawn.Data.PawnName} ({pawn.Data.Age} {genderStr})";

        // Mood with color
        float mood = pawn.Mood.CurrentMood;
        Color moodColor;
        string moodText;
        if (mood > 80f) { moodColor = new Color(0.3f, 1f, 0.4f); moodText = "振奋"; }
        else if (mood > 60f) { moodColor = new Color(0.5f, 0.9f, 0.3f); moodText = "开心"; }
        else if (mood > 40f) { moodColor = new Color(0.9f, 0.9f, 0.3f); moodText = "正常"; }
        else if (mood > 25f) { moodColor = new Color(1f, 0.6f, 0.2f); moodText = "焦虑"; }
        else { moodColor = new Color(1f, 0.3f, 0.2f); moodText = "崩溃"; }

        _moodLabel.Text = $"心情: {mood:F0} ({moodText})";
        _moodLabel.AddThemeColorOverride("font_color", moodColor);

        // AI action
        string aiText = pawn.AI?.CurrentActionName ?? "None";
        if (pawn.IsPlayerControlled) aiText = "玩家控制";
        _aiLabel.Text = $"▶ {aiText}";

        // Needs bars
        for (int i = 0; i < NeedNames.Length; i++)
        {
            float val = pawn.Needs.GetByName(NeedNames[i]);
            _needProgressBars[i].Value = val;
            _needLabels[i].Text = $"{val:F0}";

            // Change bar color when critical
            if (val < 20f)
            {
                var fill = _needProgressBars[i].GetThemeStylebox("fill") as StyleBoxFlat;
                if (fill != null)
                {
                    float flash = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.008f);
                    fill.BgColor = NeedColors[i].Lerp(new Color(1f, 0.2f, 0.2f), flash);
                }
            }
            else
            {
                var fill = _needProgressBars[i].GetThemeStylebox("fill") as StyleBoxFlat;
                if (fill != null) fill.BgColor = NeedColors[i];
            }
        }

        // Traits
        if (pawn.Data.Traits.Count > 0)
        {
            var traitTexts = new System.Collections.Generic.List<string>();
            foreach (var trait in pawn.Data.Traits)
                traitTexts.Add($"[{trait.DisplayName}]");
            _traitsLabel.Text = $"特质: {string.Join(" ", traitTexts)}";
            _traitsLabel.Visible = true;
        }
        else
        {
            _traitsLabel.Visible = false;
        }

        // Key stats
        _statsLabel.Text = $"力:{pawn.Data.GetStat("Strength"):F0} " +
                           $"智:{pawn.Data.GetStat("Intellect"):F0} " +
                           $"敏:{pawn.Data.GetStat("Agility"):F0} " +
                           $"意:{pawn.Data.GetStat("Will"):F0} | " +
                           $"挖:{pawn.Data.GetStat("Mining"):F0} " +
                           $"建:{pawn.Data.GetStat("Construction"):F0} " +
                           $"种:{pawn.Data.GetStat("Growing"):F0}";
    }

    private static HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        return sep;
    }
}
