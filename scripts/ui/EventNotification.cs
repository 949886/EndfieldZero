using System;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Top-of-screen event notification system.
/// Displays incident alerts with color-coded backgrounds.
/// Notifications stay until the player dismisses them (Right-click) or views details (Left-click).
/// </summary>
public partial class EventNotification : Control
{
    private VBoxContainer _vbox;
    private AcceptDialog _detailDialog;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        // Container for notifications
        _vbox = new VBoxContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -260f,
            OffsetTop = -600f,
            OffsetRight = -20f,
            OffsetBottom = -60f,  // A bit higher from bottom edge
            GrowHorizontal = GrowDirection.Begin,
            GrowVertical = GrowDirection.Begin,
            Alignment = BoxContainer.AlignmentMode.End, // Items stack at the bottom
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _vbox.AddThemeConstantOverride("separation", 8);
        AddChild(_vbox);

        // Dialog for details
        _detailDialog = new AcceptDialog
        {
            Title = "事件详情",
            DialogText = "加载中...",
            Exclusive = false,
        };
        AddChild(_detailDialog);

        EventBus.IncidentTriggered += OnIncidentTriggered;
        EventBus.ThreatLevelChanged += OnThreatLevelChanged;
    }

    public override void _ExitTree()
    {
        EventBus.IncidentTriggered -= OnIncidentTriggered;
        EventBus.ThreatLevelChanged -= OnThreatLevelChanged;
    }

    private void OnIncidentTriggered(string id, string displayName, string desc)
    {
        Color color = id switch
        {
            "raid" or "animal_attack" => new Color(0.9f, 0.2f, 0.2f),
            "cold_snap" or "heat_wave" or "disaster" => new Color(0.9f, 0.7f, 0.1f),
            "wanderer_joins" or "party" or "aurora" => new Color(0.2f, 0.8f, 0.3f),
            _ => new Color(0.3f, 0.6f, 0.9f),
        };

        CreateNotificationItem(id, displayName, desc, color);
    }

    private void OnThreatLevelChanged(string level)
    {
        if (level == "combat")
            OnIncidentTriggered("_combat", "⚠️ 殖民地进入战斗状态!", "由于出现敌对单位，殖民地现在处于警戒和战斗状态。");
        else if (level == "peace")
            OnIncidentTriggered("_peace", "✅ 威胁已解除，恢复和平", "所有的威胁都已经被清除，殖民地重新回归和平。");
    }

    private void CreateNotificationItem(string id, string text, string desc, Color color)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(240, 40),
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            BorderWidthLeft = 2, BorderWidthTop = 2,
            BorderWidthRight = 2, BorderWidthBottom = 2,
            BorderColor = color,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", color);
        panel.AddChild(label);

        string details = $"事件: {text}\n\n{desc}";

        panel.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    // Show details and remove
                    _detailDialog.DialogText = details;
                    _detailDialog.PopupCentered();
                    panel.QueueFree();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    // Dismiss
                    panel.QueueFree();
                }
                panel.AcceptEvent();
            }
        };

        // Add to VBox (bottom)
        _vbox.AddChild(panel);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.U)
        {
            var events = new[] { "raid", "animal_attack", "cold_snap", "disaster", "wanderer_joins", "party", "aurora" };
            var id = events[new Random().Next(events.Length)];
            
            // Delegate to Storyteller to execute actual event logic
            Storyteller.Storyteller.Instance?.ForceTriggerIncident(id);
            
            AcceptEvent();
        }
    }
}

