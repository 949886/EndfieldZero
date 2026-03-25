using System;
using EndfieldZero.Core;

namespace EndfieldZero.Environment;

/// <summary>
/// Weather types the simulation can produce.
/// </summary>
public enum WeatherType
{
    Clear,
    Cloudy,
    Rain,
    Thunderstorm,
    Snow,
    Fog,
    Heatwave
}

/// <summary>
/// Markov-chain weather state machine.
/// Weather transitions are evaluated every N game hours.
/// Transition weights depend on the current season.
/// </summary>
public static class WeatherSystem
{
    public static WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;

    private static int _hoursSinceLastChange;
    private static readonly Random _rng = new();

    // --- Per-weather modifiers ---

    /// <summary>Temperature modifier from weather (°C).</summary>
    public static float TemperatureModifier => CurrentWeather switch
    {
        WeatherType.Clear        => 0f,
        WeatherType.Cloudy       => -2f,
        WeatherType.Rain         => -5f,
        WeatherType.Thunderstorm => -8f,
        WeatherType.Snow         => -15f,
        WeatherType.Fog          => -3f,
        WeatherType.Heatwave     => 12f,
        _ => 0f,
    };

    /// <summary>Crop growth speed multiplier.</summary>
    public static float CropGrowthMultiplier => CurrentWeather switch
    {
        WeatherType.Clear        => 1.0f,
        WeatherType.Cloudy       => 0.9f,
        WeatherType.Rain         => 1.3f,
        WeatherType.Thunderstorm => 1.2f,
        WeatherType.Snow         => 0.0f,
        WeatherType.Fog          => 0.8f,
        WeatherType.Heatwave     => 0.5f,
        _ => 1.0f,
    };

    /// <summary>Movement speed multiplier for pawns.</summary>
    public static float MoveSpeedMultiplier => CurrentWeather switch
    {
        WeatherType.Clear        => 1.0f,
        WeatherType.Cloudy       => 1.0f,
        WeatherType.Rain         => 0.85f,
        WeatherType.Thunderstorm => 0.7f,
        WeatherType.Snow         => 0.6f,
        WeatherType.Fog          => 0.8f,
        WeatherType.Heatwave     => 0.9f,
        _ => 1.0f,
    };

    /// <summary>Mood offset applied to all colonists.</summary>
    public static float MoodOffset => CurrentWeather switch
    {
        WeatherType.Clear        => 5f,
        WeatherType.Cloudy       => 0f,
        WeatherType.Rain         => -5f,
        WeatherType.Thunderstorm => -10f,
        WeatherType.Snow         => -8f,
        WeatherType.Fog          => -3f,
        WeatherType.Heatwave     => -10f,
        _ => 0f,
    };

    /// <summary>
    /// Called by EnvironmentManager on each hour change.
    /// </summary>
    public static void OnHourChanged(int hour)
    {
        _hoursSinceLastChange++;
        if (_hoursSinceLastChange >= Settings.WeatherChangeIntervalHours)
        {
            _hoursSinceLastChange = 0;
            EvaluateTransition();
        }
    }

    /// <summary>Select next weather using season-weighted probabilities.</summary>
    private static void EvaluateTransition()
    {
        var weights = GetTransitionWeights(SeasonSystem.CurrentSeason);
        var total = 0f;
        foreach (var w in weights) total += w;

        var roll = (float)(_rng.NextDouble() * total);
        var cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                var newWeather = (WeatherType)i;
                if (newWeather != CurrentWeather)
                {
                    CurrentWeather = newWeather;
                    EventBus.FireWeatherChanged(CurrentWeather);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Transition weight array indexed by WeatherType ordinal.
    /// Order: Clear, Cloudy, Rain, Thunderstorm, Snow, Fog, Heatwave
    /// </summary>
    private static float[] GetTransitionWeights(Season season)
    {
        return season switch
        {
            //                       Clear  Cloudy  Rain  Thunder  Snow  Fog  Heatwave
            Season.Spring => new[] { 35f,   25f,    20f,  5f,      0f,   10f, 5f  },
            Season.Summer => new[] { 40f,   15f,    15f,  10f,     0f,   5f,  15f },
            Season.Autumn => new[] { 20f,   30f,    20f,  5f,      10f,  10f, 5f  },
            Season.Winter => new[] { 15f,   20f,    5f,   2f,      40f,  15f, 3f  },
            _ =>             new[] { 30f,   20f,    15f,  10f,     10f,  10f, 5f  },
        };
    }

    /// <summary>Reset state (for new game).</summary>
    public static void Reset()
    {
        CurrentWeather = WeatherType.Clear;
        _hoursSinceLastChange = 0;
    }
}
