using System;
using System.Collections.Generic;
using System.Linq;
using Cherry.Research;
using Godot;

namespace Cherry.EditorTools;

[Tool]
public partial class TechnologyTreeEditorDock : Control
{
    internal static readonly Vector2 NodeSize = new(230f, 104f);
    private const int MaxHistoryEntries = 128;

    private PanelContainer _graphViewport;
    private Control _graphRoot;
    private TechnologyTreeEditorLinkCanvas _linkCanvas;
    private Control _nodeLayer;

    private LineEdit _pathEdit;
    private Label _statusLabel;
    private Button _undoButton;
    private Button _redoButton;
    private Button _deleteButton;

    private LineEdit _idEdit;
    private LineEdit _nameEdit;
    private TextEdit _descriptionEdit;
    private SpinBox _researchTicksEdit;
    private Label _positionLabel;
    private LineEdit _iconPathEdit;
    private ItemList _prereqList;
    private OptionButton _prereqPicker;
    private VBoxContainer _effectsContainer;

    private FileDialog _resourceOpenDialog;
    private FileDialog _resourceSaveDialog;
    private FileDialog _iconDialog;

    private readonly Dictionary<TechnologyDef, TechnologyTreeEditorNodeCard> _nodeCards = new();

    private TechnologyTreeDef _tree;
    private TechnologyDef _selectedTechnology;
    private string _resourcePath = TechnologyTreePaths.DefaultResourcePath;
    private bool _updatingFields;
    private bool _isPanning;
    private bool _hasUnsavedChanges;
    private bool _restoringHistory;
    private TechnologyDef _dragHistoryTechnology;
    private Vector2 _panOffset;
    private float _zoom = 1f;

    private readonly List<EditorHistoryState> _undoHistory = new();
    private readonly List<EditorHistoryState> _redoHistory = new();

    public IEnumerable<TechnologyDef> Technologies => _tree?.Technologies ?? Enumerable.Empty<TechnologyDef>();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        BuildDialogs();
        LoadTree(_resourcePath);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo || !key.CtrlPressed)
            return;

        bool handled = false;
        if (key.Keycode == Key.Z && key.ShiftPressed)
        {
            handled = Redo();
        }
        else if (key.Keycode == Key.Y)
        {
            handled = Redo();
        }
        else if (key.Keycode == Key.Z)
        {
            handled = Undo();
        }

        if (handled)
            GetViewport().SetInputAsHandled();
    }

    internal TechnologyDef FindTechnologyById(string technologyId)
    {
        if (_tree == null || string.IsNullOrWhiteSpace(technologyId))
            return null;

        foreach (TechnologyDef technology in _tree.Technologies)
        {
            if (technology != null && string.Equals(technology.Id, technologyId, StringComparison.Ordinal))
                return technology;
        }

        return null;
    }

    internal bool TryGetCard(TechnologyDef technology, out TechnologyTreeEditorNodeCard card)
        => _nodeCards.TryGetValue(technology, out card);

    private void BuildUi()
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        root.AddChild(BuildToolbar());

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.SplitOffset = 900;
        root.AddChild(split);

        _graphViewport = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop,
            ClipContents = true,
        };
        _graphViewport.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.12f, 0.98f),
            BorderColor = new Color(0.24f, 0.28f, 0.36f, 1f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        });
        _graphViewport.GuiInput += OnGraphViewportInput;
        split.AddChild(_graphViewport);

        _graphRoot = new Control
        {
            CustomMinimumSize = new Vector2(1480f, 900f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _graphViewport.AddChild(_graphRoot);

        _linkCanvas = new TechnologyTreeEditorLinkCanvas(this)
        {
            CustomMinimumSize = _graphRoot.CustomMinimumSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _graphRoot.AddChild(_linkCanvas);

        _nodeLayer = new Control
        {
            CustomMinimumSize = _graphRoot.CustomMinimumSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _graphRoot.AddChild(_nodeLayer);

        split.AddChild(BuildInspectorPanel());
    }

    private Control BuildToolbar()
    {
        var toolbar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        toolbar.AddThemeConstantOverride("separation", 6);

        toolbar.AddChild(new Label
        {
            Text = "Technology Tree Editor",
            CustomMinimumSize = new Vector2(180f, 0f),
            VerticalAlignment = VerticalAlignment.Center,
        });

        _pathEdit = new LineEdit
        {
            Text = _resourcePath,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "res://resources/research/technology_tree.tres",
        };
        toolbar.AddChild(_pathEdit);

        toolbar.AddChild(CreateToolbarButton("Load", () => _resourceOpenDialog.PopupCenteredRatio(0.7f)));
        toolbar.AddChild(CreateToolbarButton("Save", SaveTree));
        toolbar.AddChild(CreateToolbarButton("Save As", () => _resourceSaveDialog.PopupCenteredRatio(0.7f)));
        _undoButton = CreateToolbarButton("Undo", () => Undo());
        _redoButton = CreateToolbarButton("Redo", () => Redo());
        toolbar.AddChild(_undoButton);
        toolbar.AddChild(_redoButton);
        toolbar.AddChild(CreateToolbarButton("Defaults", CreateDefaultTree));
        toolbar.AddChild(CreateToolbarButton("Add Node", AddTechnology));

        _deleteButton = CreateToolbarButton("Delete Node", DeleteSelectedTechnology);
        toolbar.AddChild(_deleteButton);

        toolbar.AddChild(CreateToolbarButton("Center", CenterGraphOnContent));

        _statusLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toolbar.AddChild(_statusLabel);

        return toolbar;
    }

    private Control BuildInspectorPanel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(360f, 0f),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.16f, 1f),
            BorderColor = new Color(0.24f, 0.28f, 0.36f, 1f),
            BorderWidthLeft = 1,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 14,
            ContentMarginBottom = 14,
        });

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(content);

        content.AddChild(CreateSectionLabel("Selected Technology"));

        _idEdit = CreateField(content, "Id");
        _idEdit.TextChanged += OnIdChanged;

        _nameEdit = CreateField(content, "Display Name");
        _nameEdit.TextChanged += OnNameChanged;

        content.AddChild(CreateFieldLabel("Description"));
        _descriptionEdit = new TextEdit
        {
            CustomMinimumSize = new Vector2(0f, 110f),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        _descriptionEdit.TextChanged += OnDescriptionChanged;
        content.AddChild(_descriptionEdit);

        content.AddChild(CreateFieldLabel("Research Ticks"));
        _researchTicksEdit = new SpinBox
        {
            MinValue = 1,
            MaxValue = 500000,
            Step = 10,
            Rounded = true,
        };
        _researchTicksEdit.ValueChanged += OnResearchTicksChanged;
        content.AddChild(_researchTicksEdit);

        _positionLabel = CreateFieldLabel("Canvas Position: -");
        content.AddChild(_positionLabel);

        content.AddChild(CreateFieldLabel("Icon"));
        var iconRow = new HBoxContainer();
        iconRow.AddThemeConstantOverride("separation", 6);
        content.AddChild(iconRow);

        _iconPathEdit = new LineEdit
        {
            Editable = false,
            PlaceholderText = "No custom icon",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        iconRow.AddChild(_iconPathEdit);
        iconRow.AddChild(CreateToolbarButton("Pick", () => _iconDialog.PopupCenteredRatio(0.6f)));
        iconRow.AddChild(CreateToolbarButton("Clear", ClearSelectedIcon));

        content.AddChild(CreateSectionLabel("Prerequisites"));

        _prereqList = new ItemList
        {
            CustomMinimumSize = new Vector2(0f, 120f),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        content.AddChild(_prereqList);

        var prereqRow = new HBoxContainer();
        prereqRow.AddThemeConstantOverride("separation", 6);
        content.AddChild(prereqRow);

        _prereqPicker = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        prereqRow.AddChild(_prereqPicker);
        prereqRow.AddChild(CreateToolbarButton("Add", AddPrerequisiteToSelected));
        prereqRow.AddChild(CreateToolbarButton("Remove", RemoveSelectedPrerequisite));

        content.AddChild(CreateSectionLabel("Effects"));

        _effectsContainer = new VBoxContainer();
        _effectsContainer.AddThemeConstantOverride("separation", 6);
        content.AddChild(_effectsContainer);

        content.AddChild(CreateToolbarButton("Add Effect", AddEffectToSelected));

        return panel;
    }

    private void BuildDialogs()
    {
        _resourceOpenDialog = CreateFileDialog(FileDialog.FileModeEnum.OpenFile, "Open Technology Tree", OnResourceOpenSelected);
        _resourceOpenDialog.AddFilter("*.tres,*.res ; Technology Tree Resource");

        _resourceSaveDialog = CreateFileDialog(FileDialog.FileModeEnum.SaveFile, "Save Technology Tree", OnResourceSaveSelected);
        _resourceSaveDialog.AddFilter("*.tres ; Godot Text Resource");
        _resourceSaveDialog.CurrentPath = _resourcePath;

        _iconDialog = CreateFileDialog(FileDialog.FileModeEnum.OpenFile, "Pick Icon Texture", OnIconSelected);
        _iconDialog.AddFilter("*.png,*.jpg,*.jpeg,*.webp,*.svg,*.tres,*.res ; Textures");
    }

    private FileDialog CreateFileDialog(FileDialog.FileModeEnum mode, string title, Action<string> onFileSelected)
    {
        var dialog = new FileDialog
        {
            FileMode = mode,
            Access = FileDialog.AccessEnum.Resources,
            UseNativeDialog = false,
            Title = title,
        };
        dialog.FileSelected += path => onFileSelected(path);
        AddChild(dialog);
        return dialog;
    }

    private Button CreateToolbarButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
        };
        button.Pressed += onPressed;
        return button;
    }

    private static Label CreateSectionLabel(string text)
    {
        var label = new Label
        {
            Text = text,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        return label;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
        };
    }

    private static LineEdit CreateField(VBoxContainer parent, string labelText)
    {
        parent.AddChild(CreateFieldLabel(labelText));
        var edit = new LineEdit();
        parent.AddChild(edit);
        return edit;
    }

    private void LoadTree(string path)
    {
        TechnologyTreeDef loadedTree = null;
        bool existed = ResourceLoader.Exists(path);
        if (existed)
            loadedTree = ResourceLoader.Load<TechnologyTreeDef>(path);

        _tree = TechnologyTreeUtils.CloneTree(loadedTree ?? TechnologyTreeUtils.CreateEmptyTree());
        TechnologyTreeUtils.NormalizeTree(_tree);
        _resourcePath = path;
        _pathEdit.Text = path;
        _resourceSaveDialog.CurrentPath = path;
        _hasUnsavedChanges = !existed;
        _undoHistory.Clear();
        _redoHistory.Clear();

        RebuildGraph();
        _selectedTechnology ??= _tree.Technologies.FirstOrDefault();
        RefreshSelection();
        UpdateHistoryButtons();
        CenterGraphOnContent();
        UpdateStatus(existed
            ? $"Loaded {path}"
            : "Loaded default technology tree. Save to create a resource file.");
    }

    private void CreateDefaultTree()
    {
        PushUndoSnapshot();
        _tree = TechnologyTreeUtils.CloneTree(TechnologyTreeUtils.CreateEmptyTree());
        TechnologyTreeUtils.NormalizeTree(_tree);
        _selectedTechnology = _tree.Technologies.FirstOrDefault();
        _hasUnsavedChanges = true;
        RebuildGraph();
        RefreshSelection();
        UpdateHistoryButtons();
        CenterGraphOnContent();
        UpdateStatus("Reset to default technology tree.");
    }

    private void SaveTree()
    {
        if (string.IsNullOrWhiteSpace(_pathEdit.Text))
        {
            UpdateStatus("Choose a resource path before saving.", true);
            return;
        }

        _resourcePath = _pathEdit.Text.Trim();
        if (!_resourcePath.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) &&
            !_resourcePath.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
        {
            _resourcePath += ".tres";
        }

        _pathEdit.Text = _resourcePath;
        _resourceSaveDialog.CurrentPath = _resourcePath;

        List<string> issues = ValidateTree().ToList();
        if (issues.Count > 0)
        {
            UpdateStatus($"Cannot save: {issues[0]}", true);
            return;
        }

        Error error = ResourceSaver.Save(_tree, _resourcePath);
        if (error != Error.Ok)
        {
            UpdateStatus($"Save failed: {error}", true);
            return;
        }

        _hasUnsavedChanges = false;
        UpdateStatus($"Saved {_resourcePath}");
    }

    private IEnumerable<string> ValidateTree()
    {
        if (_tree == null)
            yield return "Technology tree is empty.";

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TechnologyDef technology in Technologies)
        {
            if (technology == null)
            {
                yield return "Technology list contains a null entry.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(technology.Id))
            {
                yield return "Every technology needs a non-empty Id.";
                continue;
            }

            if (!seen.Add(technology.Id))
                yield return $"Duplicate technology Id: {technology.Id}";
        }

        foreach (TechnologyDef technology in Technologies)
        {
            if (technology == null)
                continue;

            foreach (string prereqId in technology.PrerequisiteIds)
            {
                if (!seen.Contains(prereqId))
                    yield return $"{technology.Id} references missing prerequisite {prereqId}";
            }
        }
    }

    private void AddTechnology()
    {
        if (_tree == null)
            return;

        PushUndoSnapshot();
        var technology = new TechnologyDef
        {
            Id = MakeUniqueId("new_technology"),
            DisplayName = "New Technology",
            Description = "",
            ResearchTicks = 600,
            CanvasPosition = _selectedTechnology != null
                ? _selectedTechnology.CanvasPosition + new Vector2(280f, 0f)
                : new Vector2(180f, 180f),
            PrerequisiteIds = new Godot.Collections.Array<string>(),
            Effects = new Godot.Collections.Array<TechnologyEffectDef>(),
        };

        _tree.Technologies.Add(technology);
        _selectedTechnology = technology;
        _hasUnsavedChanges = true;
        RebuildGraph();
        RefreshSelection();
        UpdateStatus($"Added {technology.Id}");
    }

    private void DeleteSelectedTechnology()
    {
        if (_tree == null || _selectedTechnology == null)
            return;

        PushUndoSnapshot();
        string deletedId = _selectedTechnology.Id;
        _tree.Technologies.Remove(_selectedTechnology);

        foreach (TechnologyDef technology in Technologies)
        {
            if (technology == null)
                continue;

            for (int index = technology.PrerequisiteIds.Count - 1; index >= 0; index--)
            {
                if (string.Equals(technology.PrerequisiteIds[index], deletedId, StringComparison.Ordinal))
                    technology.PrerequisiteIds.RemoveAt(index);
            }
        }

        _selectedTechnology = _tree.Technologies.FirstOrDefault();
        _hasUnsavedChanges = true;
        RebuildGraph();
        RefreshSelection();
        UpdateStatus($"Deleted {deletedId}");
    }

    private void AddPrerequisiteToSelected()
    {
        if (_selectedTechnology == null || _prereqPicker.ItemCount == 0)
            return;

        int index = _prereqPicker.Selected;
        if (index < 0)
            index = 0;

        var prereqId = _prereqPicker.GetItemMetadata(index).AsString();
        if (string.IsNullOrWhiteSpace(prereqId) || _selectedTechnology.PrerequisiteIds.Contains(prereqId))
            return;

        PushUndoSnapshot();
        _selectedTechnology.PrerequisiteIds.Add(prereqId);
        MarkChanged();
        RefreshSelection();
        _linkCanvas.QueueRedraw();
    }

    private void RemoveSelectedPrerequisite()
    {
        if (_selectedTechnology == null || _prereqList.GetSelectedItems().Length == 0)
            return;

        int selectedIndex = _prereqList.GetSelectedItems()[0];
        if (selectedIndex < 0 || selectedIndex >= _selectedTechnology.PrerequisiteIds.Count)
            return;

        PushUndoSnapshot();
        _selectedTechnology.PrerequisiteIds.RemoveAt(selectedIndex);
        MarkChanged();
        RefreshSelection();
        _linkCanvas.QueueRedraw();
    }

    private void AddEffectToSelected()
    {
        if (_selectedTechnology == null)
            return;

        PushUndoSnapshot();
        _selectedTechnology.Effects.Add(new TechnologyEffectDef
        {
            EffectType = TechnologyEffectType.UnlockBuilding,
            TargetId = "",
            Value = 1f,
        });
        MarkChanged();
        RefreshSelection();
    }

    private void ClearSelectedIcon()
    {
        if (_selectedTechnology == null)
            return;

        PushUndoSnapshot();
        _selectedTechnology.Icon = null;
        MarkChanged();
        RefreshSelection();
        RefreshNodeVisuals();
    }

    private void OnIdChanged(string newText)
    {
        if (_updatingFields || _selectedTechnology == null)
            return;

        string oldId = _selectedTechnology.Id;
        if (string.Equals(oldId, newText.Trim(), StringComparison.Ordinal))
            return;

        PushUndoSnapshot();
        _selectedTechnology.Id = newText.Trim();

        if (!string.Equals(oldId, _selectedTechnology.Id, StringComparison.Ordinal))
        {
            foreach (TechnologyDef technology in Technologies)
            {
                if (technology == null || technology == _selectedTechnology)
                    continue;

                for (int index = 0; index < technology.PrerequisiteIds.Count; index++)
                {
                    if (string.Equals(technology.PrerequisiteIds[index], oldId, StringComparison.Ordinal))
                        technology.PrerequisiteIds[index] = _selectedTechnology.Id;
                }
            }
        }

        MarkChanged();
        RebuildGraph();
        RefreshSelection();
    }

    private void OnNameChanged(string newText)
    {
        if (_updatingFields || _selectedTechnology == null)
            return;

        if (string.Equals(_selectedTechnology.DisplayName, newText, StringComparison.Ordinal))
            return;

        PushUndoSnapshot();
        _selectedTechnology.DisplayName = newText;
        MarkChanged();
        RefreshNodeVisuals();
    }

    private void OnDescriptionChanged()
    {
        if (_updatingFields || _selectedTechnology == null)
            return;

        if (string.Equals(_selectedTechnology.Description, _descriptionEdit.Text, StringComparison.Ordinal))
            return;

        PushUndoSnapshot();
        _selectedTechnology.Description = _descriptionEdit.Text;
        MarkChanged();
    }

    private void OnResearchTicksChanged(double newValue)
    {
        if (_updatingFields || _selectedTechnology == null)
            return;

        int roundedValue = Math.Max(1, (int)Math.Round(newValue));
        if (_selectedTechnology.ResearchTicks == roundedValue)
            return;

        PushUndoSnapshot();
        _selectedTechnology.ResearchTicks = roundedValue;
        MarkChanged();
        RefreshNodeVisuals();
    }

    private void OnResourceOpenSelected(string path)
    {
        LoadTree(path);
    }

    private void OnResourceSaveSelected(string path)
    {
        _pathEdit.Text = path;
        SaveTree();
    }

    private void OnIconSelected(string path)
    {
        if (_selectedTechnology == null)
            return;

        Texture2D texture = ResourceLoader.Load<Texture2D>(path);
        if (_selectedTechnology.Icon == texture)
            return;

        PushUndoSnapshot();
        _selectedTechnology.Icon = texture;
        MarkChanged();
        RefreshSelection();
        RefreshNodeVisuals();
    }

    private void OnGraphViewportInput(InputEvent @event)
    {
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
        float newZoom = Mathf.Clamp(_zoom * factor, 0.5f, 1.8f);
        if (Mathf.IsEqualApprox(oldZoom, newZoom))
            return;

        Vector2 graphPoint = (pivot - _panOffset) / oldZoom;
        _zoom = newZoom;
        _panOffset = pivot - graphPoint * _zoom;
        ApplyGraphTransform();
    }

    private void ApplyGraphTransform()
    {
        _graphRoot.Position = _panOffset;
        _graphRoot.Scale = new Vector2(_zoom, _zoom);
        _linkCanvas.QueueRedraw();
    }

    private void RebuildGraph()
    {
        foreach (Node child in _nodeLayer.GetChildren())
            child.QueueFree();

        _nodeCards.Clear();

        Vector2 graphSize = new Vector2(1480f, 900f);
        foreach (TechnologyDef technology in Technologies)
        {
            if (technology == null)
                continue;

            var card = new TechnologyTreeEditorNodeCard(
                technology,
                () => _zoom,
                OnTechnologySelected,
                OnTechnologyDragStarted,
                OnTechnologyDragged,
                OnTechnologyDragEnded)
            {
                Position = technology.CanvasPosition,
                Size = NodeSize,
                CustomMinimumSize = NodeSize,
            };
            _nodeLayer.AddChild(card);
            _nodeCards[technology] = card;

            graphSize.X = Mathf.Max(graphSize.X, technology.CanvasPosition.X + NodeSize.X + 160f);
            graphSize.Y = Mathf.Max(graphSize.Y, technology.CanvasPosition.Y + NodeSize.Y + 160f);
        }

        _graphRoot.CustomMinimumSize = graphSize;
        _nodeLayer.CustomMinimumSize = graphSize;
        _linkCanvas.CustomMinimumSize = graphSize;
        RefreshNodeVisuals();
    }

    private void RefreshSelection()
    {
        RefreshInspector();
        RefreshNodeVisuals();
        _deleteButton.Disabled = _selectedTechnology == null;
    }

    private void RefreshInspector()
    {
        _updatingFields = true;

        bool hasSelection = _selectedTechnology != null;
        _idEdit.Editable = hasSelection;
        _nameEdit.Editable = hasSelection;
        _descriptionEdit.Editable = hasSelection;
        _researchTicksEdit.Editable = hasSelection;

        if (!hasSelection)
        {
            _idEdit.Text = "";
            _nameEdit.Text = "";
            _descriptionEdit.Text = "";
            _researchTicksEdit.Value = 1;
            _positionLabel.Text = "Canvas Position: -";
            _iconPathEdit.Text = "";
            _prereqList.Clear();
            _prereqPicker.Clear();
            ClearEffectsUi();
            _updatingFields = false;
            return;
        }

        _idEdit.Text = _selectedTechnology.Id;
        _nameEdit.Text = _selectedTechnology.DisplayName;
        _descriptionEdit.Text = _selectedTechnology.Description;
        _researchTicksEdit.Value = _selectedTechnology.ResearchTicks;
        _positionLabel.Text = $"Canvas Position: {_selectedTechnology.CanvasPosition.X:F0}, {_selectedTechnology.CanvasPosition.Y:F0}";
        _iconPathEdit.Text = _selectedTechnology.Icon?.ResourcePath ?? "";

        _prereqList.Clear();
        foreach (string prereqId in _selectedTechnology.PrerequisiteIds)
            _prereqList.AddItem(prereqId);

        _prereqPicker.Clear();
        foreach (TechnologyDef technology in Technologies)
        {
            if (technology == null || technology == _selectedTechnology)
                continue;

            if (_selectedTechnology.PrerequisiteIds.Contains(technology.Id))
                continue;

            int itemIndex = _prereqPicker.ItemCount;
            string label = string.IsNullOrWhiteSpace(technology.DisplayName) ? technology.Id : $"{technology.DisplayName} ({technology.Id})";
            _prereqPicker.AddItem(label);
            _prereqPicker.SetItemMetadata(itemIndex, technology.Id);
        }

        RebuildEffectsUi();
        _updatingFields = false;
    }

    private void RebuildEffectsUi()
    {
        ClearEffectsUi();
        if (_selectedTechnology == null)
            return;

        for (int index = 0; index < _selectedTechnology.Effects.Count; index++)
        {
            TechnologyEffectDef effect = _selectedTechnology.Effects[index];
            if (effect == null)
                continue;

            _effectsContainer.AddChild(BuildEffectRow(effect));
        }
    }

    private void ClearEffectsUi()
    {
        foreach (Node child in _effectsContainer.GetChildren())
            child.QueueFree();
    }

    private Control BuildEffectRow(TechnologyEffectDef effect)
    {
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 6);
        row.AddChild(top);

        var typePicker = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        foreach (TechnologyEffectType effectType in Enum.GetValues(typeof(TechnologyEffectType)))
            typePicker.AddItem(effectType.ToString());
        typePicker.Selected = (int)effect.EffectType;
        typePicker.ItemSelected += selected =>
        {
            if (_updatingFields)
                return;

            PushUndoSnapshot();
            effect.EffectType = (TechnologyEffectType)selected;
            MarkChanged();
        };
        top.AddChild(typePicker);

        var removeButton = CreateToolbarButton("Remove", () =>
        {
            PushUndoSnapshot();
            _selectedTechnology?.Effects.Remove(effect);
            MarkChanged();
            RefreshSelection();
        });
        top.AddChild(removeButton);

        row.AddChild(CreateFieldLabel("Target Id"));
        var targetEdit = new LineEdit
        {
            Text = effect.TargetId,
            PlaceholderText = "Building Id or empty for multipliers",
        };
        targetEdit.TextChanged += newText =>
        {
            if (_updatingFields)
                return;

            if (string.Equals(effect.TargetId, newText.Trim(), StringComparison.Ordinal))
                return;

            PushUndoSnapshot();
            effect.TargetId = newText.Trim();
            MarkChanged();
        };
        row.AddChild(targetEdit);

        row.AddChild(CreateFieldLabel("Value"));
        var valueEdit = new SpinBox
        {
            MinValue = -10,
            MaxValue = 10,
            Step = 0.05,
            Value = effect.Value,
        };
        valueEdit.ValueChanged += newValue =>
        {
            if (_updatingFields)
                return;

            float nextValue = (float)newValue;
            if (Mathf.IsEqualApprox(effect.Value, nextValue))
                return;

            PushUndoSnapshot();
            effect.Value = (float)newValue;
            MarkChanged();
        };
        row.AddChild(valueEdit);

        var separator = new HSeparator();
        row.AddChild(separator);
        return row;
    }

    private void RefreshNodeVisuals()
    {
        HashSet<string> duplicateIds = FindDuplicateIds();
        foreach ((TechnologyDef technology, TechnologyTreeEditorNodeCard card) in _nodeCards)
        {
            bool hasError = technology == null ||
                            string.IsNullOrWhiteSpace(technology.Id) ||
                            duplicateIds.Contains(technology.Id);
            card.Position = technology?.CanvasPosition ?? Vector2.Zero;
            card.Refresh(technology, technology == _selectedTechnology, hasError);
        }

        _linkCanvas.QueueRedraw();
    }

    private HashSet<string> FindDuplicateIds()
    {
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TechnologyDef technology in Technologies)
        {
            if (technology == null || string.IsNullOrWhiteSpace(technology.Id))
                continue;

            if (!seen.Add(technology.Id))
                duplicates.Add(technology.Id);
        }

        return duplicates;
    }

    private void OnTechnologySelected(TechnologyDef technology)
    {
        _selectedTechnology = technology;
        RefreshSelection();
    }

    private void OnTechnologyDragStarted(TechnologyDef technology)
    {
        if (technology == null || _dragHistoryTechnology == technology)
            return;

        PushUndoSnapshot();
        _dragHistoryTechnology = technology;
    }

    private void OnTechnologyDragged(TechnologyDef technology, Vector2 delta)
    {
        if (technology == null)
            return;

        technology.CanvasPosition += delta;
        if (technology == _selectedTechnology)
            _positionLabel.Text = $"Canvas Position: {technology.CanvasPosition.X:F0}, {technology.CanvasPosition.Y:F0}";

        _hasUnsavedChanges = true;
        RefreshNodeVisuals();
        ExpandGraphBoundsIfNeeded(technology);
    }

    private void OnTechnologyDragEnded(TechnologyDef technology)
    {
        if (_dragHistoryTechnology == technology)
            _dragHistoryTechnology = null;
    }

    private void ExpandGraphBoundsIfNeeded(TechnologyDef technology)
    {
        if (technology == null)
            return;

        Vector2 size = _graphRoot.CustomMinimumSize;
        size.X = Mathf.Max(size.X, technology.CanvasPosition.X + NodeSize.X + 160f);
        size.Y = Mathf.Max(size.Y, technology.CanvasPosition.Y + NodeSize.Y + 160f);
        _graphRoot.CustomMinimumSize = size;
        _nodeLayer.CustomMinimumSize = size;
        _linkCanvas.CustomMinimumSize = size;
    }

    private void CenterGraphOnContent()
    {
        float viewportWidth = _graphViewport.Size.X;
        float viewportHeight = _graphViewport.Size.Y;
        float graphWidth = _graphRoot.CustomMinimumSize.X;
        float graphHeight = _graphRoot.CustomMinimumSize.Y;
        _zoom = 1f;
        _panOffset = new Vector2(
            Mathf.Max(40f, (viewportWidth - graphWidth) * 0.5f),
            Mathf.Max(40f, (viewportHeight - graphHeight) * 0.22f));
        ApplyGraphTransform();
    }

    private string MakeUniqueId(string baseId)
    {
        string candidate = baseId;
        int suffix = 1;
        while (Technologies.Any(technology => technology != null && string.Equals(technology.Id, candidate, StringComparison.Ordinal)))
        {
            suffix++;
            candidate = $"{baseId}_{suffix}";
        }

        return candidate;
    }

    private void MarkChanged()
    {
        _hasUnsavedChanges = true;
        UpdateHistoryButtons();
        UpdateStatus();
    }

    private bool Undo()
    {
        if (_undoHistory.Count == 0 || _tree == null)
            return false;

        _redoHistory.Add(CreateHistoryState());
        RestoreHistoryState(_undoHistory[^1]);
        _undoHistory.RemoveAt(_undoHistory.Count - 1);
        UpdateStatus("Undo");
        return true;
    }

    private bool Redo()
    {
        if (_redoHistory.Count == 0 || _tree == null)
            return false;

        _undoHistory.Add(CreateHistoryState());
        RestoreHistoryState(_redoHistory[^1]);
        _redoHistory.RemoveAt(_redoHistory.Count - 1);
        UpdateStatus("Redo");
        return true;
    }

    private void PushUndoSnapshot()
    {
        if (_tree == null || _restoringHistory)
            return;

        _undoHistory.Add(CreateHistoryState());
        if (_undoHistory.Count > MaxHistoryEntries)
            _undoHistory.RemoveAt(0);

        _redoHistory.Clear();
        UpdateHistoryButtons();
    }

    private EditorHistoryState CreateHistoryState()
    {
        return new EditorHistoryState(
            TechnologyTreeUtils.CloneTree(_tree),
            _selectedTechnology?.Id ?? "",
            _hasUnsavedChanges);
    }

    private void RestoreHistoryState(EditorHistoryState state)
    {
        if (state == null)
            return;

        _restoringHistory = true;
        _tree = TechnologyTreeUtils.CloneTree(state.Tree);
        TechnologyTreeUtils.NormalizeTree(_tree);
        _selectedTechnology = _tree.Technologies.FirstOrDefault(technology =>
            technology != null &&
            string.Equals(technology.Id, state.SelectedTechnologyId, StringComparison.Ordinal))
            ?? _tree.Technologies.FirstOrDefault();
        _hasUnsavedChanges = state.HasUnsavedChanges;
        _dragHistoryTechnology = null;
        RebuildGraph();
        RefreshSelection();
        UpdateHistoryButtons();
        _restoringHistory = false;
    }

    private void UpdateHistoryButtons()
    {
        if (_undoButton != null)
            _undoButton.Disabled = _undoHistory.Count == 0;
        if (_redoButton != null)
            _redoButton.Disabled = _redoHistory.Count == 0;
    }

    private void UpdateStatus(string message = null, bool isError = false)
    {
        string dirty = _hasUnsavedChanges ? "Unsaved changes" : "Saved";
        List<string> issues = ValidateTree().ToList();
        string summary = issues.Count == 0 ? dirty : $"{dirty} | {issues.Count} issue(s)";

        if (!string.IsNullOrWhiteSpace(message))
            summary = $"{summary} | {message}";

        _statusLabel.Text = summary;
        _statusLabel.Modulate = isError ? new Color(1f, 0.72f, 0.72f) : Colors.White;
    }

    private sealed class EditorHistoryState
    {
        public EditorHistoryState(TechnologyTreeDef tree, string selectedTechnologyId, bool hasUnsavedChanges)
        {
            Tree = tree;
            SelectedTechnologyId = selectedTechnologyId;
            HasUnsavedChanges = hasUnsavedChanges;
        }

        public TechnologyTreeDef Tree { get; }
        public string SelectedTechnologyId { get; }
        public bool HasUnsavedChanges { get; }
    }
}

[Tool]
internal sealed partial class TechnologyTreeEditorNodeCard : PanelContainer
{
    private readonly TechnologyDef _technology;
    private readonly Func<float> _zoomProvider;
    private readonly Action<TechnologyDef> _onSelected;
    private readonly Action<TechnologyDef> _onDragStarted;
    private readonly Action<TechnologyDef, Vector2> _onDragged;
    private readonly Action<TechnologyDef> _onDragEnded;
    private readonly TextureRect _icon;
    private readonly Label _title;
    private readonly Label _meta;
    private readonly StyleBoxFlat _panelStyle;

    private bool _dragging;

    public TechnologyTreeEditorNodeCard(
        TechnologyDef technology,
        Func<float> zoomProvider,
        Action<TechnologyDef> onSelected,
        Action<TechnologyDef> onDragStarted,
        Action<TechnologyDef, Vector2> onDragged,
        Action<TechnologyDef> onDragEnded)
    {
        _technology = technology;
        _zoomProvider = zoomProvider;
        _onSelected = onSelected;
        _onDragStarted = onDragStarted;
        _onDragged = onDragged;
        _onDragEnded = onDragEnded;
        MouseFilter = MouseFilterEnum.Stop;

        _panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.14f, 0.18f, 0.97f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.31f, 0.36f, 0.46f, 0.9f),
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
            CustomMinimumSize = new Vector2(64f, 64f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        root.AddChild(_icon);

        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 4);
        root.AddChild(column);

        _title = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _title.AddThemeFontSizeOverride("font_size", 16);
        _title.AddThemeColorOverride("font_color", Colors.White);
        column.AddChild(_title);

        _meta = new Label();
        _meta.AddThemeFontSizeOverride("font_size", 12);
        _meta.AddThemeColorOverride("font_color", new Color(0.78f, 0.8f, 0.88f));
        column.AddChild(_meta);

        GuiInput += OnGuiInput;
    }

    public void Refresh(TechnologyDef technology, bool selected, bool hasError)
    {
        _icon.Texture = TechnologyTreeUtils.GetDisplayIcon(technology);
        _title.Text = string.IsNullOrWhiteSpace(technology?.DisplayName) ? "(Unnamed Technology)" : technology.DisplayName;
        _meta.Text = technology == null
            ? "Invalid"
            : $"{technology.Id} | {technology.ResearchTicks} ticks";

        _panelStyle.BorderColor = hasError
            ? new Color(0.98f, 0.44f, 0.44f)
            : selected
                ? Colors.White
                : new Color(0.32f, 0.74f, 0.93f, 0.9f);
        _panelStyle.BorderWidthLeft = selected ? 3 : 2;
        _panelStyle.BorderWidthTop = selected ? 3 : 2;
        _panelStyle.BorderWidthRight = selected ? 3 : 2;
        _panelStyle.BorderWidthBottom = selected ? 3 : 2;
        _panelStyle.BgColor = hasError
            ? new Color(0.23f, 0.11f, 0.12f, 0.98f)
            : selected
                ? new Color(0.12f, 0.18f, 0.24f, 0.98f)
                : new Color(0.13f, 0.14f, 0.18f, 0.97f);
    }

    private void OnGuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left && mb.Pressed:
                _dragging = true;
                _onSelected?.Invoke(_technology);
                AcceptEvent();
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left && !mb.Pressed:
                _onDragEnded?.Invoke(_technology);
                _dragging = false;
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                float zoom = Mathf.Max(0.001f, _zoomProvider?.Invoke() ?? 1f);
                _onDragStarted?.Invoke(_technology);
                _onDragged?.Invoke(_technology, motion.Relative / zoom);
                AcceptEvent();
                break;
        }
    }
}

[Tool]
internal sealed partial class TechnologyTreeEditorLinkCanvas : Control
{
    private readonly TechnologyTreeEditorDock _owner;

    public TechnologyTreeEditorLinkCanvas(TechnologyTreeEditorDock owner)
    {
        _owner = owner;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        foreach (TechnologyDef technology in _owner.Technologies)
        {
            if (technology == null)
                continue;

            foreach (string prereqId in technology.PrerequisiteIds)
            {
                TechnologyDef parent = _owner.FindTechnologyById(prereqId);
                if (parent == null ||
                    !_owner.TryGetCard(parent, out TechnologyTreeEditorNodeCard parentCard) ||
                    !_owner.TryGetCard(technology, out TechnologyTreeEditorNodeCard childCard))
                {
                    continue;
                }

                Vector2 start = parentCard.Position + new Vector2(TechnologyTreeEditorDock.NodeSize.X, TechnologyTreeEditorDock.NodeSize.Y * 0.5f);
                Vector2 end = childCard.Position + new Vector2(0f, TechnologyTreeEditorDock.NodeSize.Y * 0.5f);
                Vector2[] points = BuildLinkPoints(start, end);
                DrawPolyline(points, new Color(0.64f, 0.72f, 0.84f, 0.88f), 5f, true);
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

        AddRoundedSegment(points, start, cornerA, cornerB, 20f, true);
        AddRoundedSegment(points, cornerA, cornerB, end, 20f, false);
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
        float actualRadius = Mathf.Min(radius, Mathf.Min(inLen * 0.5f, outLen * 0.5f));

        Vector2 arcStart = corner - dirIn * actualRadius;
        Vector2 arcEnd = corner + dirOut * actualRadius;

        if (includeStart)
            points.Add(previous);

        points.Add(arcStart);

        Vector2 center = arcStart + dirOut * actualRadius;
        float startAngle = (arcStart - center).Angle();
        float endAngle = (arcEnd - center).Angle();
        float delta = Mathf.Wrap(endAngle - startAngle, -Mathf.Pi, Mathf.Pi);

        const int steps = 8;
        for (int step = 1; step < steps; step++)
        {
            float angle = startAngle + delta * (step / (float)steps);
            points.Add(center + Vector2.FromAngle(angle) * actualRadius);
        }

        points.Add(arcEnd);
    }
}
