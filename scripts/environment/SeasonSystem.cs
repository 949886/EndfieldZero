using EndfieldZero.Core;

namespace EndfieldZero.Environment;

/// <summary>
/// Season enum — Spring → Summer → Autumn → Winter cycle.
/// </summary>
public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

/// <summary>
/// Determines the current season from the game day counter.
/// Each season lasts <see cref="Settings.DaysPerSeason"/> days.
/// </summary>
public static class SeasonSystem
{
    /// <summary>Current season.</summary>
    public static Season CurrentSeason { get; private set; } = Season.Spring;

    private static int _lastDay = -1;

    /// <summary>Base temperature modifier per season (°C).</summary>
    public static float BaseTemperatureModifier => CurrentSeason switch
    {
        Season.Spring => 0f,
        Season.Summer => 15f,
        Season.Autumn => -5f,
        Season.Winter => -20f,
        _ => 0f,
    };

    /// <summary>
    /// Called by EnvironmentManager every time the game day changes.
    /// Recalculates the season and fires an event on transition.
    /// </summary>
    public static void OnDayChanged(int day)
    {
        if (day == _lastDay) return;
        _lastDay = day;

        int dps = Settings.DaysPerSeason;
        // Day 1-based: cycle index = (day - 1) / dps % 4
        int seasonIndex = ((day - 1) / dps) % 4;
        var newSeason = (Season)seasonIndex;

        if (newSeason != CurrentSeason)
        {
            CurrentSeason = newSeason;
            EventBus.FireSeasonChanged(CurrentSeason);
        }
    }

    /// <summary>Reset state (for new game).</summary>
    public static void Reset()
    {
        CurrentSeason = Season.Spring;
        _lastDay = -1;
    }
}
