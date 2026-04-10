using EndfieldZero.Core;

namespace EndfieldZero.Environment;

/// <summary>
/// Calculates temperature by combining biome base, season, weather, and time-of-day modifiers.
/// Supports temporary weather offsets (cold snap / heat wave).
/// </summary>
public class TemperatureSystem
{
    /// <summary>Baseline reference temperature (°C) for a temperate biome at noon.</summary>
    private const float BaseTempC = 20f;

    public static TemperatureSystem Instance { get; set; } = new();

    private float _tempOffset;
    private long _offsetExpireTick;

    /// <summary>
    /// Apply a temporary temperature offset (e.g. -20 for cold snap) for a duration in ticks.
    /// </summary>
    public void ApplyTemperatureOffset(float offsetC, long durationTicks)
    {
        _tempOffset = offsetC;
        long currentTick = TimeManager.Instance?.CurrentTick ?? 0;
        _offsetExpireTick = currentTick + durationTicks;
    }

    /// <summary>Current active temperature offset.</summary>
    public float CurrentOffset
    {
        get
        {
            long tick = TimeManager.Instance?.CurrentTick ?? 0;
            return tick < _offsetExpireTick ? _tempOffset : 0f;
        }
    }

    /// <summary>
    /// Get the ambient temperature (no biome factor).
    /// Combines season + weather + time-of-day + incident offset modifiers.
    /// </summary>
    public static float GetAmbientTemperature(int currentHour)
    {
        float temp = BaseTempC;
        temp += SeasonSystem.BaseTemperatureModifier;
        temp += WeatherSystem.TemperatureModifier;
        temp += GetTimeOfDayModifier(currentHour);
        temp += Instance.CurrentOffset;
        return temp;
    }

    /// <summary>
    /// Get temperature at a specific world block coordinate.
    /// Adds a biome-based temperature offset on top of the ambient temperature.
    /// </summary>
    public static float GetTemperature(float biomeTemperatureNoise, int currentHour)
    {
        float biomeOffset = biomeTemperatureNoise * 15f;
        return GetAmbientTemperature(currentHour) + biomeOffset;
    }

    /// <summary>
    /// Time-of-day temperature swing:
    /// Night (22–6):  −5 °C
    /// Morning (6–10): linearly ramp from −5 to 0
    /// Midday (10–14): +3 °C
    /// Afternoon (14–18): linearly ramp from +3 to 0
    /// Evening (18–22): linearly ramp from 0 to −5
    /// </summary>
    private static float GetTimeOfDayModifier(int hour)
    {
        return hour switch
        {
            >= 6 and < 10 => Lerp(-5f, 0f, (hour - 6) / 4f),
            >= 10 and < 14 => 3f,
            >= 14 and < 18 => Lerp(3f, 0f, (hour - 14) / 4f),
            >= 18 and < 22 => Lerp(0f, -5f, (hour - 18) / 4f),
            _ => -5f,
        };
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
