namespace EndfieldZero.Storyteller;

/// <summary>
/// Static definition for a story event / incident.
/// Categories: MajorThreat, Weather, RandomGood, RandomBad
/// </summary>
public class IncidentDef
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }         // MajorThreat, Weather, RandomGood, RandomBad
    public float MinThreatPoints { get; }   // minimum colony threat to trigger
    public float Weight { get; }            // selection weight within category
    public int CooldownDays { get; }        // days before same incident can fire again
    public int MinDayToTrigger { get; }     // earliest game day to trigger
    public float DurationDays { get; }      // for weather/timed events

    public IncidentDef(string id, string displayName, string description, string category,
        float minThreatPoints = 0f, float weight = 1f,
        int cooldownDays = 5, int minDayToTrigger = 1,
        float durationDays = 0f)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        MinThreatPoints = minThreatPoints;
        Weight = weight;
        CooldownDays = cooldownDays;
        MinDayToTrigger = minDayToTrigger;
        DurationDays = durationDays;
    }
}
