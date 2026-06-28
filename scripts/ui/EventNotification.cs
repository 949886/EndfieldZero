using System;
using Cherry.Core;
using Godot;

namespace Cherry.UI;

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

        _vbox = new VBoxContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -260f,
            OffsetTop = -600f,
            OffsetRight = -20f,
            OffsetBottom = -60f,
            GrowHorizontal = GrowDirection.Begin,
            GrowVertical = GrowDirection.Begin,
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _vbox.AddThemeConstantOverride("separation", 8);
        AddChild(_vbox);

        _detailDialog = new AcceptDialog
        {
            Title = "\u4e8b\u4ef6\u8be6\u60c5",
            DialogText = "\u52a0\u8f7d\u4e2d...",
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
            "raid" or "animal_attack" or "_assault_start" => new Color(0.9f, 0.2f, 0.2f),
            "_alert" or "cold_snap" or "heat_wave" or "disaster" => new Color(0.9f, 0.7f, 0.1f),
            "_peace" or "wanderer_joins" or "party" or "aurora" => new Color(0.2f, 0.8f, 0.3f),
            _ => new Color(0.3f, 0.6f, 0.9f),
        };

        CreateNotificationItem(displayName, desc, color);
    }

    private void OnThreatLevelChanged(string level)
    {
        if (level == "alert")
        {
            OnIncidentTriggered(
                "_alert",
                "\u654c\u4eba\u6b63\u5728\u96c6\u7ed3",
                "\u654c\u5bf9\u5355\u4f4d\u6b63\u5728\u5916\u56f4\u5f98\u5f8a\u548c\u96c6\u7ed3\uff0c\u968f\u65f6\u53ef\u80fd\u5411\u6b96\u6c11\u5730\u53d1\u8d77\u8fdb\u653b\u3002");
        }
        else if (level == "peace")
        {
            OnIncidentTriggered(
                "_peace",
                "\u5a01\u80c1\u5df2\u89e3\u9664\uff0c\u6062\u590d\u548c\u5e73",
                "\u6240\u6709\u7684\u5a01\u80c1\u90fd\u5df2\u7ecf\u88ab\u6e05\u9664\uff0c\u6b96\u6c11\u5730\u91cd\u65b0\u56de\u5f52\u548c\u5e73\u3002");
        }
    }

    private void CreateNotificationItem(string text, string desc, Color color)
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
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
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

        string details = $"\u4e8b\u4ef6: {text}\n\n{desc}";

        panel.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _detailDialog.DialogText = details;
                    _detailDialog.PopupCentered();
                    panel.QueueFree();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    panel.QueueFree();
                }

                panel.AcceptEvent();
            }
        };

        _vbox.AddChild(panel);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.U)
        {
            var events = new[] { "raid", "animal_attack", "cold_snap", "disaster", "wanderer_joins", "party", "aurora" };
            var id = events[new Random().Next(events.Length)];

            Storyteller.Storyteller.Instance?.ForceTriggerIncident(id);
            AcceptEvent();
        }
    }
}
