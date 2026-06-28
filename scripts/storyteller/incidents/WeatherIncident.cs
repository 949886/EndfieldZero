using Cherry.Core;
using Cherry.Environment;
using Godot;

namespace Cherry.Storyteller.Incidents;

/// <summary>
/// Weather incidents: Cold Snap and Heat Wave.
/// Integrates with TemperatureSystem via temperature offset.
/// Affects crops and pawn mood.
/// </summary>
public class WeatherIncident : IncidentWorker
{
    public override void Execute(IncidentDef def, float threatPoints)
    {
        switch (def.Id)
        {
            case "cold_snap":
                ExecuteColdSnap(def);
                break;
            case "heat_wave":
                ExecuteHeatWave(def);
                break;
        }
    }

    private void ExecuteColdSnap(IncidentDef def)
    {
        GD.Print($"[Weather] ❄️ Cold Snap! Duration: {def.DurationDays:F1} days");

        // Apply temperature offset via TemperatureSystem
        if (TemperatureSystem.Instance != null)
        {
            TemperatureSystem.Instance.ApplyTemperatureOffset(-20f,
                (long)(def.DurationDays * TimeManager.TicksPerHour * 24));
        }

        // Kill some crops
        if (Farming.CropManager.Instance != null)
        {
            int killed = Farming.CropManager.Instance.DamageRandomCrops(0.3f);
            GD.Print($"[Weather] {killed} crops damaged by cold");
        }

        // Mood penalty to all colonists
        if (Managers.PawnManager.Instance != null)
        {
            foreach (var pawn in Managers.PawnManager.Instance.GetAllPawns())
            {
                if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
                {
                    pawn.Mood.AddThought("cold_snap", "寒潮来袭", -8f,
                        (int)(def.DurationDays * TimeManager.TicksPerHour * 24));
                }
            }
        }
    }

    private void ExecuteHeatWave(IncidentDef def)
    {
        GD.Print($"[Weather] 🔥 Heat Wave! Duration: {def.DurationDays:F1} days");

        if (TemperatureSystem.Instance != null)
        {
            TemperatureSystem.Instance.ApplyTemperatureOffset(20f,
                (long)(def.DurationDays * TimeManager.TicksPerHour * 24));
        }

        // Mood penalty
        if (Managers.PawnManager.Instance != null)
        {
            foreach (var pawn in Managers.PawnManager.Instance.GetAllPawns())
            {
                if (pawn.Data.Faction == "Colony" && pawn.IsAlive)
                {
                    pawn.Mood.AddThought("heat_wave", "热浪炎炎", -6f,
                        (int)(def.DurationDays * TimeManager.TicksPerHour * 24));
                }
            }
        }
    }
}
