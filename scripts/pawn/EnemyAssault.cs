using Godot;

namespace Cherry.Pawn;

public enum EnemyAssaultMode
{
    ImmediateAttack,
    DelayedAttack,
}

public enum EnemyAssaultPhase
{
    Preparing,
    Assaulting,
    Fleeing,
}

public sealed class HostileWaveContext
{
    private static int _nextWaveId = 1;

    public int WaveId { get; }
    public string IncidentId { get; }
    public EnemyAssaultMode AssaultMode { get; }
    public long PrepareUntilTick { get; }
    public Vector3 RallyCenter { get; }
    public bool AssaultNotificationSent { get; set; }

    public HostileWaveContext(string incidentId, EnemyAssaultMode assaultMode, long prepareUntilTick, Vector3 rallyCenter)
    {
        WaveId = _nextWaveId++;
        IncidentId = incidentId;
        AssaultMode = assaultMode;
        PrepareUntilTick = prepareUntilTick;
        RallyCenter = rallyCenter;
    }
}
