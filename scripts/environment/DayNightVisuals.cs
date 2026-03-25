using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.Environment;

/// <summary>
/// Screen-space overlay that tints the viewport based on time-of-day and weather.
/// Add as a child of a CanvasLayer so it renders on top of the game world.
/// </summary>
public partial class DayNightVisuals : ColorRect
{
    // --- Tint colours ---
    private static readonly Color DawnTint  = new(1.0f, 0.7f, 0.3f, 0.25f);
    private static readonly Color DayTint   = new(1.0f, 1.0f, 1.0f, 0.0f);
    private static readonly Color DuskTint  = new(1.0f, 0.55f, 0.25f, 0.30f);
    private static readonly Color NightTint = new(0.15f, 0.15f, 0.45f, 0.30f);

    // --- Weather overlay colours ---
    private static readonly Color RainOverlay  = new(0.4f, 0.45f, 0.55f, 0.15f);
    private static readonly Color SnowOverlay  = new(0.9f, 0.92f, 0.95f, 0.20f);
    private static readonly Color FogOverlay   = new(0.85f, 0.85f, 0.85f, 0.25f);
    private static readonly Color ClearOverlay = new(1f, 1f, 1f, 0f);

    /// <summary>Current fractional hour used for smooth interpolation.</summary>
    private float _smoothHour;

    public override void _Ready()
    {
        // Fill entire viewport
        AnchorsPreset = (int)LayoutPreset.FullRect;
        MouseFilter = MouseFilterEnum.Ignore;
        Color = DayTint;
    }

    public override void _Process(double delta)
    {
        var time = TimeManager.Instance;
        if (time == null) return;

        // Smooth hour: integer hour + fractional tick within this hour
        int tickInHour = (int)(time.CurrentTick % TimeManager.TicksPerHour);
        _smoothHour = time.CurrentHour + (float)tickInHour / TimeManager.TicksPerHour;

        Color timeTint = GetTimeTint(_smoothHour);
        Color weatherOverlay = GetWeatherOverlay(WeatherSystem.CurrentWeather);

        // Blend: additive overlay — multiply the weather colour into the time tint
        Color finalColor = BlendOverlays(timeTint, weatherOverlay);
        Color = finalColor;
    }

    /// <summary>
    /// Get time-of-day tint based on smooth fractional hour.
    /// Dawn 5–7 | Day 7–17 | Dusk 17–19 | Night 19–5
    /// Transitions are linearly interpolated.
    /// </summary>
    private static Color GetTimeTint(float hour)
    {
        return hour switch
        {
            >= 5f and < 6f   => NightTint.Lerp(DawnTint, hour - 5f),
            >= 6f and < 7f   => DawnTint.Lerp(DayTint, hour - 6f),
            >= 7f and < 17f  => DayTint,
            >= 17f and < 18f => DayTint.Lerp(DuskTint, hour - 17f),
            >= 18f and < 19f => DuskTint.Lerp(NightTint, hour - 18f),
            _                => NightTint,
        };
    }

    /// <summary>Get a faint colour overlay based on weather.</summary>
    private static Color GetWeatherOverlay(WeatherType weather)
    {
        return weather switch
        {
            WeatherType.Rain         => RainOverlay,
            WeatherType.Thunderstorm => RainOverlay with { A = 0.22f },
            WeatherType.Snow         => SnowOverlay,
            WeatherType.Fog          => FogOverlay,
            _ => ClearOverlay,
        };
    }

    /// <summary>Simple alpha-composite two colours (over operator).</summary>
    private static Color BlendOverlays(Color baseC, Color overlay)
    {
        float outA = overlay.A + baseC.A * (1f - overlay.A);
        if (outA <= 0f) return new Color(0, 0, 0, 0);

        float outR = (overlay.R * overlay.A + baseC.R * baseC.A * (1f - overlay.A)) / outA;
        float outG = (overlay.G * overlay.A + baseC.G * baseC.A * (1f - overlay.A)) / outA;
        float outB = (overlay.B * overlay.A + baseC.B * baseC.A * (1f - overlay.A)) / outA;

        return new Color(outR, outG, outB, outA);
    }
}
