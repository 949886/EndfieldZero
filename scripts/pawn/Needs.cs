namespace EndfieldZero.Pawn;

/// <summary>
/// Tracks a pawn's current need values. All values range 0-100.
/// Needs decay over time; individual rates can be modified by traits.
///
/// Thresholds:
///   > 80: Satisfied (positive mood)
///   50-80: Normal
///   20-50: Unsatisfied (negative mood)
///   &lt; 20: Critical (severe mood penalty, potential mental break)
///   = 0:  Extreme (Hunger → death, Rest → collapse)
/// </summary>
public class Needs
{
    public float Hunger  { get; set; } = 100f;
    public float Rest    { get; set; } = 100f;
    public float Joy     { get; set; } = 100f;
    public float Comfort { get; set; } = 100f;
    public float Beauty  { get; set; } = 100f;
    public float Social  { get; set; } = 100f;

    // --- Decay rates per tick at 1× speed ---
    // 可被 Trait 修改
    public float HungerDecay  { get; set; } = 0.015f;   // ~110 分钟满→空
    public float RestDecay    { get; set; } = 0.012f;    // ~140 分钟
    public float JoyDecay     { get; set; } = 0.008f;    // ~210 分钟
    public float ComfortDecay { get; set; } = 0.005f;    // 按环境动态
    public float BeautyDecay  { get; set; } = 0.003f;    // 按环境动态
    public float SocialDecay  { get; set; } = 0.006f;    // ~280 分钟

    /// <summary>Tick all needs: apply decay, clamp to [0, 100].</summary>
    public void Tick()
    {
        Hunger  = Clamp(Hunger  - HungerDecay);
        Rest    = Clamp(Rest    - RestDecay);
        Joy     = Clamp(Joy     - JoyDecay);
        Comfort = Clamp(Comfort - ComfortDecay);
        Beauty  = Clamp(Beauty  - BeautyDecay);
        Social  = Clamp(Social  - SocialDecay);
    }

    /// <summary>Get the most urgent need (lowest value).</summary>
    public (string Name, float Value) GetMostUrgent()
    {
        string name = "Hunger";
        float min = Hunger;

        if (Rest < min) { min = Rest; name = "Rest"; }
        if (Joy < min) { min = Joy; name = "Joy"; }
        if (Comfort < min) { min = Comfort; name = "Comfort"; }
        if (Beauty < min) { min = Beauty; name = "Beauty"; }
        if (Social < min) { min = Social; name = "Social"; }

        return (name, min);
    }

    /// <summary>Check if any need is critical (&lt; 20).</summary>
    public bool HasCritical()
    {
        return Hunger < 20f || Rest < 20f || Joy < 20f
            || Comfort < 20f || Beauty < 20f || Social < 20f;
    }

    /// <summary>Get a need value by name.</summary>
    public float GetByName(string name)
    {
        return name switch
        {
            "Hunger" => Hunger, "Rest" => Rest, "Joy" => Joy,
            "Comfort" => Comfort, "Beauty" => Beauty, "Social" => Social,
            _ => 100f,
        };
    }

    /// <summary>Set a need value by name.</summary>
    public void SetByName(string name, float value)
    {
        switch (name)
        {
            case "Hunger": Hunger = Clamp(value); break;
            case "Rest": Rest = Clamp(value); break;
            case "Joy": Joy = Clamp(value); break;
            case "Comfort": Comfort = Clamp(value); break;
            case "Beauty": Beauty = Clamp(value); break;
            case "Social": Social = Clamp(value); break;
        }
    }

    private static float Clamp(float v) => v < 0f ? 0f : v > 100f ? 100f : v;
}
