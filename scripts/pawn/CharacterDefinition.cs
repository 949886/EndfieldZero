using Godot;

namespace Cherry.Pawn;

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

    [ExportGroup("Ranged Presentation")]
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene MuzzleFlashScene { get; set; }
    [Export] public PackedScene HitImpactScene { get; set; }
    [Export] public PackedScene MissImpactScene { get; set; }
    [Export(PropertyHint.Range, "1.0,200.0,0.1")] public float ProjectileSpeed { get; set; } = 48f;
    [Export(PropertyHint.Range, "0.1,5.0,0.05")] public float ProjectileMaxLifetime { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0.01,0.5,0.01")] public float ProjectileSize { get; set; } = 0.04f;
    [Export(PropertyHint.Range, "0.01,0.5,0.01")] public float ProjectileTrailWidth { get; set; } = 0.08f;
    [Export] public Color ProjectileColor { get; set; } = new(0.52f, 0.89f, 1f, 1f);
    [Export] public Color MuzzleFlashColor { get; set; } = new(1f, 0.84f, 0.5f, 1f);
    [Export] public Color HitImpactColor { get; set; } = new(1f, 0.52f, 0.22f, 1f);
    [Export] public Color MissImpactColor { get; set; } = new(0.7f, 0.82f, 1f, 1f);
}
