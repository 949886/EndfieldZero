using System;
using System.Collections.Generic;
using System.Linq;
using Cherry.Core;
using Cherry.Research;
using Godot;

namespace Cherry.UI;

public partial class TechnologyTreeOverlay : Control
{
    internal static readonly Vector2 NodeSize = new(230f, 104f);

    private PanelContainer _graphViewport;
    private PanelContainer _detailsPanel;
    private Control _graphRoot;
    private TechnologyLinkCanvas _graphCanvas;
    private Control _nodeLayer;

    private TextureRect _detailIcon;
    private Label _titleLabel;
    private Label _statusLabel;
    private Label _timeLabel;
    private Label _descriptionLabel;
    private Label _prereqLabel;
    private Label _effectsLabel;
    private Label _progressLabel;
    private ProgressBar _progressBar;
    private Button _primaryButton;
    private Label _hintLabel;

    private readonly Dictionary<string, TechnologyNodeCard> _nodeCards = new(StringComparer.Ordinal);

    private TechnologyDef _selectedTechnology;
    private bool _isPanning;
    private Vector2 _panOffset;
    private float _zoom = 1f;
    private float _resumeGameSpeed = 1f;
    private bool _hadResumeSpeed;

    public static TechnologyTreeOverlay Instance { get; private set; }
    public static bool IsOpen => Instance?.Visible == true;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _graphViewport = GetNode<PanelContainer>("GraphViewport");
        _detailsPanel = GetNode<PanelContainer>("DetailsPanel");

        BuildGraphViewport();
        BuildDetailsPanel();

        if (TechnologyManager.Instance != null)
            TechnologyManager.Instance.Changed += OnTechnologyChanged;

        Visible = false;
        RefreshUi();
    }

    public override void _ExitTree()
    {
        if (TechnologyManager.Instance != null)
            TechnologyManager.Instance.Changed -= OnTechnologyChanged;

        if (Instance == this)
            Instance = null;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (SettingsOverlay.IsOpen)
            return;

        if (Visible)
        {
            if (key.Keycode == Key.Escape)
                CloseOverlay();

            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode != Key.T)
            return;

        OpenOverlay();
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
            return;

        if (@event is InputEventMouseButton || @event is InputEventMouseMotion || @event is InputEventKey)
            GetViewport().SetInputAsHandled();
    }

    private void BuildGraphViewport()
    {
        _graphViewport.MouseFilter = MouseFilterEnum.Stop;
        _graphViewport.ClipContents = true;
        _graphViewport.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.1f, 0.94f),
            BorderColor = new Color(0.24f, 0.31f, 0.42f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        });

        _graphRoot = new Control
        {
            CustomMinimumSize = new Vector2(1480f, 900f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _graphViewport.AddChild(_graphRoot);

        _graphCanvas = new TechnologyLinkCanvas(this)
        {
            CustomMinimumSize = _graphRoot.CustomMinimumSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _graphRoot.AddChild(_graphCanvas);

        _nodeLayer = new Control
        {
            CustomMinimumSize = _graphRoot.CustomMinimumSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _graphRoot.AddChild(_nodeLayer);

        BuildTechnologyNodes();
        ApplyGraphTransform();

        _graphViewport.GuiInput += HandleGraphViewportInput;
    }

    private void BuildTechnologyNodes()
    {
        _nodeCards.Clear();

        if (TechnologyManager.Instance == null)
            return;

        Vector2 graphSize = new Vector2(1480f, 900f);
        foreach (TechnologyDef tech in TechnologyManager.Instance.AllTechnologies)
        {
            var card = new TechnologyNodeCard(tech, OnTechnologySelected)
            {
                Position = tech.CanvasPosition,
                Size = NodeSize,
                CustomMinimumSize = NodeSize,
            };
            _nodeLayer.AddChild(card);
            _nodeCards[tech.Id] = card;
            graphSize.X = Mathf.Max(graphSize.X, tech.CanvasPosition.X + NodeSize.X + 160f);
            graphSize.Y = Mathf.Max(graphSize.Y, tech.CanvasPosition.Y + NodeSize.Y + 160f);
        }

        _graphRoot.CustomMinimumSize = graphSize;
        _graphCanvas.CustomMinimumSize = graphSize;
        _nodeLayer.CustomMinimumSize = graphSize;
    }

    private void BuildDetailsPanel()
    {
        _detailsPanel.MouseFilter = MouseFilterEnum.Stop;
        _detailsPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.11f, 0.14f, 0.98f),
            BorderColor = new Color(0.27f, 0.34f, 0.46f, 0.95f),
            BorderWidthTop = 1,
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 18,
            ContentMarginBottom = 18,
        });

        var root = new HBoxContainer();
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 18);
        _detailsPanel.AddChild(root);

        _detailIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(96f, 96f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        root.AddChild(_detailIcon);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 6);
        root.AddChild(content);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(titleRow);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleRow.AddChild(_titleLabel);

        _statusLabel = new Label();
        _statusLabel.AddThemeFontSizeOverride("font_size", 16);
        titleRow.AddChild(_statusLabel);

        _timeLabel = new Label();
        _timeLabel.AddThemeFontSizeOverride("font_size", 13);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.84f, 0.92f));
        content.AddChild(_timeLabel);

        _descriptionLabel = CreateBodyLabel();
        content.AddChild(_descriptionLabel);

        _prereqLabel = CreateBodyLabel();
        content.AddChild(_prereqLabel);

        _effectsLabel = CreateBodyLabel();
        content.AddChild(_effectsLabel);

        _progressLabel = CreateBodyLabel();
        content.AddChild(_progressLabel);

        _progressBar = new ProgressBar
        {
            ShowPercentage = false,
            MinValue = 0f,
            MaxValue = 1f,
            Value = 0f,
            CustomMinimumSize = new Vector2(0f, 18f),
        };
        _progressBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.18f, 0.24f),
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
        _progressBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color(0.35f, 0.8f, 0.63f),
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
        content.AddChild(_progressBar);

        var actionColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220f, 0f),
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        actionColumn.AddThemeConstantOverride("separation", 10);
        root.AddChild(actionColumn);

        _primaryButton = new Button
        {
            Text = "开始研究",
            CustomMinimumSize = new Vector2(220f, 52f),
        };
        _primaryButton.AddThemeFontSizeOverride("font_size", 18);
        _primaryButton.Pressed += OnPrimaryActionPressed;
        actionColumn.AddChild(_primaryButton);

        _hintLabel = CreateBodyLabel();
        _hintLabel.CustomMinimumSize = new Vector2(220f, 0f);
        actionColumn.AddChild(_hintLabel);
    }

    private Label CreateBodyLabel()
    {
        var label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(0.8f, 0.82f, 0.9f));
        return label;
    }

    private void HandleGraphViewportInput(InputEvent @event)
    {
        if (!Visible)
            return;

        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                _isPanning = mb.Pressed;
                AcceptEvent();
                break;
            case InputEventMouseButton mb when mb.Pressed &&
                                               (mb.ButtonIndex == MouseButton.WheelUp ||
                                                mb.ButtonIndex == MouseButton.WheelDown):
                ApplyZoom(mb.Position, mb.ButtonIndex == MouseButton.WheelUp ? 1.1f : 0.9f);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _isPanning:
                _panOffset += motion.Relative;
                ApplyGraphTransform();
                AcceptEvent();
                break;
        }
    }

    private void ApplyZoom(Vector2 pivot, float factor)
    {
        float oldZoom = _zoom;
        float newZoom = Mathf.Clamp(_zoom * factor, 0.65f, 1.65f);
        if (Mathf.IsEqualApprox(oldZoom, newZoom))
            return;

        Vector2 graphPoint = (pivot - _panOffset) / oldZoom;
        _zoom = newZoom;
        _panOffset = pivot - graphPoint * _zoom;
        ApplyGraphTransform();
    }

    private void ApplyGraphTransform()
    {
        if (_graphRoot == null)
            return;

        _graphRoot.Position = _panOffset;
        _graphRoot.Scale = new Vector2(_zoom, _zoom);
        _graphCanvas?.QueueRedraw();
    }

    private void OnTechnologySelected(TechnologyDef tech)
    {
        _selectedTechnology = tech;
        RefreshUi();
    }

    private void OnPrimaryActionPressed()
    {
        if (_selectedTechnology == null || TechnologyManager.Instance == null)
            return;

        if (TechnologyManager.Instance.StartResearch(_selectedTechnology.Id))
            RefreshUi();
    }

    private void OpenOverlay()
    {
        if (Visible)
            return;

        PauseTime();
        Visible = true;
        ChooseDefaultSelection();
        RefreshUi();
        CallDeferred(nameof(CenterGraphOnContent));
    }

    private void CloseOverlay()
    {
        if (!Visible)
            return;

        Visible = false;
        _isPanning = false;
        ResumeTime();
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

    private void ChooseDefaultSelection()
    {
        if (TechnologyManager.Instance == null)
            return;

        if (_selectedTechnology != null && TechnologyManager.Instance.GetTechnology(_selectedTechnology.Id) != null)
            return;

        _selectedTechnology = TechnologyManager.Instance.GetTechnology(TechnologyManager.Instance.ActiveTechnologyId)
            ?? TechnologyManager.Instance.AllTechnologies.FirstOrDefault(tech => TechnologyManager.Instance.GetState(tech.Id) != TechnologyState.Locked)
            ?? TechnologyManager.Instance.AllTechnologies.FirstOrDefault();
    }

    private void CenterGraphOnContent()
    {
        if (_graphViewport == null)
            return;

        _zoom = 1f;
        float viewportWidth = _graphViewport.Size.X;
        float viewportHeight = _graphViewport.Size.Y;
        float graphWidth = _graphRoot.CustomMinimumSize.X;
        float graphHeight = _graphRoot.CustomMinimumSize.Y;
        _panOffset = new Vector2(
            Mathf.Max(40f, (viewportWidth - graphWidth) * 0.5f),
            Mathf.Max(40f, (viewportHeight - graphHeight) * 0.22f));
        ApplyGraphTransform();
    }

    private void RefreshUi()
    {
        if (TechnologyManager.Instance == null)
            return;

        ChooseDefaultSelection();

        foreach ((string techId, TechnologyNodeCard card) in _nodeCards)
        {
            card.Refresh(
                TechnologyManager.Instance.GetState(techId),
                _selectedTechnology != null && techId == _selectedTechnology.Id,
                TechnologyManager.Instance.GetProgressRatio(techId));
        }

        _graphCanvas?.QueueRedraw();
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (_selectedTechnology == null || TechnologyManager.Instance == null)
        {
            _titleLabel.Text = "Technology";
            _statusLabel.Text = "";
            _timeLabel.Text = "";
            _descriptionLabel.Text = "Select a technology node to inspect its details.";
            _prereqLabel.Text = "";
            _effectsLabel.Text = "";
            _progressLabel.Text = "";
            _progressBar.Value = 0f;
            _primaryButton.Disabled = true;
            _primaryButton.Text = "开始研究";
            _hintLabel.Text = "";
            return;
        }

        TechnologyState state = TechnologyManager.Instance.GetState(_selectedTechnology.Id);
        float progress = TechnologyManager.Instance.GetProgressRatio(_selectedTechnology.Id);

        _detailIcon.Texture = TechnologyTreeUtils.GetDisplayIcon(_selectedTechnology);
        _titleLabel.Text = _selectedTechnology.DisplayName;
        _statusLabel.Text = state switch
        {
            TechnologyState.Completed => "已完成",
            TechnologyState.InProgress => "研究中",
            TechnologyState.Available => "可研究",
            _ => "未解锁",
        };
        _statusLabel.AddThemeColorOverride("font_color", state switch
        {
            TechnologyState.Completed => new Color(0.55f, 0.95f, 0.73f),
            TechnologyState.InProgress => new Color(0.43f, 0.83f, 0.98f),
            TechnologyState.Available => new Color(0.92f, 0.84f, 0.42f),
            _ => new Color(0.62f, 0.64f, 0.72f),
        });
        _timeLabel.Text = $"研究耗时: {FormatResearchDuration(_selectedTechnology.ResearchTicks)}";
        _descriptionLabel.Text = _selectedTechnology.Description;
        _prereqLabel.Text = $"前置科技: {TechnologyManager.Instance.DescribePrerequisites(_selectedTechnology)}";
        _effectsLabel.Text = $"效果: {TechnologyManager.Instance.DescribeEffects(_selectedTechnology)}";
        _progressLabel.Text = $"进度: {progress * 100f:F0}%";
        _progressBar.Value = progress;

        bool canStart = TechnologyManager.Instance.CanStartResearch(_selectedTechnology.Id, out string reason);
        bool isActive = string.Equals(TechnologyManager.Instance.ActiveTechnologyId, _selectedTechnology.Id, StringComparison.Ordinal);
        if (state == TechnologyState.Completed)
        {
            _primaryButton.Text = "已完成";
            _primaryButton.Disabled = true;
            _hintLabel.Text = "这项科技已经生效。";
        }
        else if (state == TechnologyState.Locked)
        {
            _primaryButton.Text = "前置未满足";
            _primaryButton.Disabled = true;
            _hintLabel.Text = "先完成前置科技后才能开始研究。";
        }
        else if (isActive)
        {
            _primaryButton.Text = "继续研究";
            _primaryButton.Disabled = true;
            _hintLabel.Text = TechnologyManager.Instance.HasResearchDesk
                ? "研究作业会自动在已建成的研究台附近生成。"
                : "当前项目已选定，但缺少已建成的研究台，研究会暂停。";
        }
        else if (!TechnologyManager.Instance.HasResearchDesk)
        {
            _primaryButton.Text = "需要研究台";
            _primaryButton.Disabled = true;
            _hintLabel.Text = "先建造并完成研究台，才能开始新的科技研究。";
        }
        else
        {
            _primaryButton.Text = "开始研究";
            _primaryButton.Disabled = !canStart;
            _hintLabel.Text = string.IsNullOrEmpty(reason)
                ? "首版仅支持同时进行 1 个研究项目。"
                : reason == "已有研究项目"
                    ? "已有研究项目在进行中，请先完成当前项目。"
                    : "点击开始研究后会自动生成研究作业。";
        }
    }

    private void OnTechnologyChanged()
    {
        RefreshUi();
    }

    private static string FormatResearchDuration(int researchTicks)
    {
        float seconds = researchTicks / (float)Settings.TicksPerSecond;
        return $"{seconds:F1} 秒";
    }

    internal bool TryGetCard(string technologyId, out TechnologyNodeCard card)
        => _nodeCards.TryGetValue(technologyId, out card);
}

internal sealed partial class TechnologyNodeCard : PanelContainer
{
    private readonly TechnologyDef _technology;
    private readonly Action<TechnologyDef> _onSelected;
    private readonly TextureRect _icon;
    private readonly Label _title;
    private readonly Label _meta;
    private readonly ColorRect _progressFill;
    private readonly StyleBoxFlat _panelStyle;

    public TechnologyNodeCard(TechnologyDef technology, Action<TechnologyDef> onSelected)
    {
        _technology = technology;
        _onSelected = onSelected;
        MouseFilter = MouseFilterEnum.Stop;

        _panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.14f, 0.18f, 0.96f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.28f, 0.31f, 0.41f, 0.8f),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
        AddThemeStyleboxOverride("panel", _panelStyle);

        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        _icon = new TextureRect
        {
            Texture = TechnologyTreeUtils.GetDisplayIcon(technology),
            CustomMinimumSize = new Vector2(64f, 64f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        root.AddChild(_icon);

        var textColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        textColumn.AddThemeConstantOverride("separation", 4);
        root.AddChild(textColumn);

        _title = new Label
        {
            Text = technology.DisplayName,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _title.AddThemeFontSizeOverride("font_size", 16);
        _title.AddThemeColorOverride("font_color", Colors.White);
        textColumn.AddChild(_title);

        _meta = new Label();
        _meta.AddThemeFontSizeOverride("font_size", 12);
        _meta.AddThemeColorOverride("font_color", new Color(0.78f, 0.8f, 0.88f));
        textColumn.AddChild(_meta);

        var progressBg = new ColorRect
        {
            Color = new Color(0.19f, 0.21f, 0.28f),
            CustomMinimumSize = new Vector2(0f, 10f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        textColumn.AddChild(progressBg);

        _progressFill = new ColorRect
        {
            Color = new Color(0.35f, 0.8f, 0.63f),
            CustomMinimumSize = new Vector2(0f, 10f),
            SizeFlagsHorizontal = SizeFlags.Fill,
            AnchorRight = 0f,
        };
        progressBg.AddChild(_progressFill);
        _progressFill.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);

        GuiInput += OnGuiInput;
    }

    public void Refresh(TechnologyState state, bool selected, float progress)
    {
        _meta.Text = state switch
        {
            TechnologyState.Completed => "已完成",
            TechnologyState.InProgress => $"研究中 {progress * 100f:F0}%",
            TechnologyState.Available => "可研究",
            _ => "前置未满足",
        };

        Color accent = state switch
        {
            TechnologyState.Completed => new Color(0.42f, 0.86f, 0.66f),
            TechnologyState.InProgress => new Color(0.36f, 0.78f, 0.98f),
            TechnologyState.Available => new Color(0.9f, 0.78f, 0.34f),
            _ => new Color(0.47f, 0.49f, 0.56f),
        };

        _panelStyle.BorderColor = selected ? Colors.White : accent;
        _panelStyle.BorderWidthLeft = selected ? 3 : 2;
        _panelStyle.BorderWidthTop = selected ? 3 : 2;
        _panelStyle.BorderWidthRight = selected ? 3 : 2;
        _panelStyle.BorderWidthBottom = selected ? 3 : 2;
        _panelStyle.BgColor = state switch
        {
            TechnologyState.Completed => new Color(0.12f, 0.18f, 0.16f, 0.97f),
            TechnologyState.InProgress => new Color(0.12f, 0.17f, 0.22f, 0.97f),
            TechnologyState.Available => new Color(0.17f, 0.16f, 0.11f, 0.97f),
            _ => new Color(0.13f, 0.14f, 0.18f, 0.97f),
        };
        QueueRedraw();

        _progressFill.Size = new Vector2(Mathf.Max(0f, (TechnologyTreeOverlay.NodeSize.X - 98f) * progress), 10f);
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            _onSelected?.Invoke(_technology);
            AcceptEvent();
        }
    }
}

internal sealed partial class TechnologyLinkCanvas : Control
{
    private readonly TechnologyTreeOverlay _owner;

    public TechnologyLinkCanvas(TechnologyTreeOverlay owner)
    {
        _owner = owner;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (TechnologyManager.Instance == null)
            return;

        foreach (TechnologyDef tech in TechnologyManager.Instance.AllTechnologies)
        {
            foreach (string prereqId in tech.PrerequisiteIds)
            {
                if (!_owner.TryGetCard(prereqId, out TechnologyNodeCard parentCard) ||
                    !_owner.TryGetCard(tech.Id, out TechnologyNodeCard childCard))
                {
                    continue;
                }

                Vector2 start = parentCard.Position + new Vector2(parentCard.Size.X, parentCard.Size.Y * 0.5f);
                Vector2 end = childCard.Position + new Vector2(0f, childCard.Size.Y * 0.5f);
                var state = TechnologyManager.Instance.GetState(tech.Id);
                Color color = state switch
                {
                    TechnologyState.Completed => new Color(0.42f, 0.86f, 0.66f, 0.9f),
                    TechnologyState.InProgress => new Color(0.36f, 0.78f, 0.98f, 0.9f),
                    TechnologyState.Available => new Color(0.9f, 0.78f, 0.34f, 0.85f),
                    _ => new Color(0.38f, 0.4f, 0.48f, 0.7f),
                };

                Vector2[] points = BuildLinkPoints(start, end);
                DrawPolyline(points, color, 5f, true);
            }
        }
    }

    private static Vector2[] BuildLinkPoints(Vector2 start, Vector2 end)
    {
        List<Vector2> points = new();

        if (Mathf.Abs(start.Y - end.Y) < 1f)
        {
            points.Add(start);
            points.Add(end);
            return points.ToArray();
        }

        float midX = (start.X + end.X) * 0.5f;
        Vector2 cornerA = new(midX, start.Y);
        Vector2 cornerB = new(midX, end.Y);

        AddRoundedSegment(points, start, cornerA, cornerB, 20f, includeStart: true);
        AddRoundedSegment(points, cornerA, cornerB, end, 20f, includeStart: false);
        points.Add(end);
        return points.ToArray();
    }

    private static void AddRoundedSegment(
        List<Vector2> points,
        Vector2 previous,
        Vector2 corner,
        Vector2 next,
        float radius,
        bool includeStart)
    {
        Vector2 dirIn = (corner - previous).Normalized();
        Vector2 dirOut = (next - corner).Normalized();
        float inLen = previous.DistanceTo(corner);
        float outLen = corner.DistanceTo(next);
        float effectiveRadius = Mathf.Min(radius, Mathf.Min(inLen * 0.5f, outLen * 0.5f));
        if (effectiveRadius <= 0.1f)
        {
            if (includeStart)
                points.Add(previous);
            points.Add(corner);
            return;
        }

        Vector2 startTangent = corner - dirIn * effectiveRadius;
        Vector2 endTangent = corner + dirOut * effectiveRadius;
        if (includeStart)
            points.Add(previous);
        points.Add(startTangent);

        const int segments = 8;
        for (int i = 1; i < segments; i++)
        {
            float t = i / (float)segments;
            Vector2 sample = QuadraticBezier(startTangent, corner, endTangent, t);
            points.Add(sample);
        }

        points.Add(endTangent);
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * start
               + 2f * oneMinusT * t * control
               + t * t * end;
    }
}
