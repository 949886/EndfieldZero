using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Environment;

/// <summary>
/// Central orchestrator for the environment simulation.
/// Subscribes to TimeManager events and delegates to Season, Weather,
/// Temperature, and DayNightVisuals sub-systems.
/// </summary>
public partial class EnvironmentManager : Node
{
    /// <summary>Singleton instance.</summary>
    public static EnvironmentManager Instance { get; private set; }

    // --- Convenience accessors ---

    /// <summary>Current season.</summary>
    public Season CurrentSeason => SeasonSystem.CurrentSeason;

    /// <summary>Current weather.</summary>
    public WeatherType CurrentWeather => WeatherSystem.CurrentWeather;

    /// <summary>Ambient temperature (no biome factor).</summary>
    public float AmbientTemperature =>
        TemperatureSystem.GetAmbientTemperature(TimeManager.Instance?.CurrentHour ?? 12);

    /// <summary>
    /// Daylight factor: 1 = full day, 0 = deep night.
    /// Useful for systems that need a 0–1 linear brightness value.
    /// </summary>
    public float DaylightFactor
    {
        get
        {
            int hour = TimeManager.Instance?.CurrentHour ?? 12;
            return hour switch
            {
                >= 7 and < 17  => 1.0f,
                >= 5 and < 7   => (hour - 5) / 2f,
                >= 17 and < 19 => 1f - (hour - 17) / 2f,
                _              => 0f,
            };
        }
    }

    public override void _Ready()
    {
        Instance = this;

        EventBus.HourChanged += OnHourChanged;
        EventBus.DayChanged  += OnDayChanged;

        // Initialise sub-systems with current time state
        var time = TimeManager.Instance;
        if (time != null)
        {
            SeasonSystem.OnDayChanged(time.CurrentDay);
        }
    }

    public override void _ExitTree()
    {
        EventBus.HourChanged -= OnHourChanged;
        EventBus.DayChanged  -= OnDayChanged;

        if (Instance == this) Instance = null;
    }

    private void OnHourChanged(int hour)
    {
        WeatherSystem.OnHourChanged(hour);
    }

    private void OnDayChanged(int day)
    {
        SeasonSystem.OnDayChanged(day);
    }
}
