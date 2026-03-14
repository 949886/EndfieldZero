using System;

namespace EndfieldZero.Core;

/// <summary>
/// Global event bus for decoupled communication between systems.
/// All events are static C# events. Subscribe in _Ready, unsubscribe in _ExitTree.
/// </summary>
public static class EventBus
{
    // --- Pawn lifecycle ---
    public static event Action<int> PawnSpawned;
    public static event Action<int> PawnDied;
    public static event Action<int> PawnMentalBreak;

    // --- Needs ---
    public static event Action<int, string> NeedCritical;     // pawnId, needName
    public static event Action<int, string> NeedSatisfied;    // pawnId, needName

    // --- Jobs ---
    public static event Action<int> JobCreated;
    public static event Action<int> JobCompleted;
    public static event Action<int> JobFailed;

    // --- Time ---
    public static event Action<long> Tick;            // current tick number
    public static event Action<int> HourChanged;       // game hour (0-23)
    public static event Action<int> DayChanged;        // game day

    // --- Fire methods ---
    public static void FirePawnSpawned(int id) => PawnSpawned?.Invoke(id);
    public static void FirePawnDied(int id) => PawnDied?.Invoke(id);
    public static void FirePawnMentalBreak(int id) => PawnMentalBreak?.Invoke(id);
    public static void FireNeedCritical(int pawnId, string need) => NeedCritical?.Invoke(pawnId, need);
    public static void FireNeedSatisfied(int pawnId, string need) => NeedSatisfied?.Invoke(pawnId, need);
    public static void FireJobCreated(int jobId) => JobCreated?.Invoke(jobId);
    public static void FireJobCompleted(int jobId) => JobCompleted?.Invoke(jobId);
    public static void FireJobFailed(int jobId) => JobFailed?.Invoke(jobId);
    public static void FireTick(long tick) => Tick?.Invoke(tick);
    public static void FireHourChanged(int hour) => HourChanged?.Invoke(hour);
    public static void FireDayChanged(int day) => DayChanged?.Invoke(day);
}
