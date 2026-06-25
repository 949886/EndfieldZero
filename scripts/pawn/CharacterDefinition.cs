using Godot;

namespace EndfieldZero.Pawn;

[GlobalClass]
public partial class CharacterDefinition : Resource
{
    [Export] public string CharacterId { get; set; } = "miyu";

    [ExportGroup("Core Locomotion")]
    [Export] public string IdleAnimation { get; set; } = "";
    [Export] public string MoveAnimation { get; set; } = "";
    [Export] public string MoveStopAnimation { get; set; } = "";
    [Export] public string WorkAnimation { get; set; } = "";
    [Export] public string WateringAnimation { get; set; } = "";

    [ExportGroup("Standing Combat")]
    [Export] public string AttackStartAnimation { get; set; } = "";
    [Export] public string AttackLoopAnimation { get; set; } = "";
    [Export] public string AttackDelayAnimation { get; set; } = "";
    [Export] public string AttackEndAnimation { get; set; } = "";
    [Export] public string ReloadAnimation { get; set; } = "";

    [ExportGroup("Kneeling Combat")]
    [Export] public bool UseKneelAttack { get; set; }
    [Export] public float KneelMinDistanceBlocks { get; set; } = 6f;
    [Export] public string KneelIdleAnimation { get; set; } = "";
    [Export] public string KneelAttackStartAnimation { get; set; } = "";
    [Export] public string KneelAttackLoopAnimation { get; set; } = "";
    [Export] public string KneelAttackDelayAnimation { get; set; } = "";
    [Export] public string KneelAttackEndAnimation { get; set; } = "";
    [Export] public string KneelReloadAnimation { get; set; } = "";

    [ExportGroup("Vital")]
    [Export] public string DeathAnimation { get; set; } = "";
    [Export] public string DyingAnimation { get; set; } = "";
    [Export] public string RetreatAnimation { get; set; } = "";
    [Export] public string PanicAnimation { get; set; } = "";

    [ExportGroup("Timeline")]
    [Export(PropertyHint.Range, "0.0,5.0,0.01")] public float AttackFireTimeSeconds { get; set; } = 0.15f;
    [Export(PropertyHint.Range, "0.0,5.0,0.01")] public float KneelAttackFireTimeSeconds { get; set; } = 0.15f;
}
