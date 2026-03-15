using Godot;

namespace EndfieldZero.Core;

/// <summary>
/// Manages game time: tick counting, speed control, and time-of-day.
/// Add as autoload or attach to the main scene.
/// Fires EventBus.Tick every game tick, scaled by game speed.
/// </summary>
public partial class TimeManager : Node
{
    /// <summary>Game speed multiplier. 0 = paused, 1 = normal, 2 = fast, 3 = fastest.</summary>
    [Export] public float GameSpeed { get; set; } = 1f;

    /// <summary>Total ticks elapsed since game start.</summary>
    public long CurrentTick { get; private set; }

    /// <summary>Ticks per in-game hour.</summary>
    public const int TicksPerHour = 2500;     // ~42 秒现实时间 = 1 小时游戏时间

    /// <summary>Hours per in-game day.</summary>
    public const int HoursPerDay = 24;

    /// <summary>Current in-game hour (0-23).</summary>
    public int CurrentHour => (int)(CurrentTick / TicksPerHour) % HoursPerDay;

    /// <summary>Current in-game day (starts at 1).</summary>
    public int CurrentDay => (int)(CurrentTick / (TicksPerHour * HoursPerDay)) + 1;

    private double _accumulator;
    private int _lastHour = -1;
    private int _lastDay = -1;

    /// <summary>Singleton instance for easy access.</summary>
    public static TimeManager Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        if (GameSpeed <= 0f) return;

        _accumulator += delta * GameSpeed;

        while (_accumulator >= Settings.TickInterval)
        {
            _accumulator -= Settings.TickInterval;
            CurrentTick++;
            EventBus.FireTick(CurrentTick);

            // Check hour/day changes
            int hour = CurrentHour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                EventBus.FireHourChanged(hour);

                int day = CurrentDay;
                if (day != _lastDay)
                {
                    _lastDay = day;
                    EventBus.FireDayChanged(day);
                }
            }
        }
    }

    /// <summary>Speed control: pause / 1× / 2× / 3×.</summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            switch (key.Keycode)
            {
                case Key.Space:
                    GameSpeed = GameSpeed > 0 ? 0f : 1f;
                    break;
                case Key.Key1:
                    GameSpeed = 1f;
                    break;
                case Key.Key2:
                    GameSpeed = 2f;
                    break;
                case Key.Key3:
                    GameSpeed = 3f;
                    break;
            }
        }
    }
}
