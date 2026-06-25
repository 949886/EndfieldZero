using Godot;

namespace EndfieldZero.Pawn;

public partial class Pawn3D : Pawn
{
    [Export] public CharacterDefinition CharacterDefinition { get; set; }
    [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");
    [Export] public NodePath ModelRootPath { get; set; } = new("VisualRoot/Miyu");
    [Export] public NodePath AnimationPlayerPath { get; set; } = new("VisualRoot/Miyu/AnimationPlayer2");
    [Export] public NodePath HeadAnchorPath { get; set; } = new("VisualRoot/HeadAnchor");
    [Export] public Vector3 ModelScale { get; set; } = Vector3.One;
    [Export] public Vector3 ModelOffset { get; set; } = Vector3.Zero;
    [Export] public float ModelYawOffsetDegrees { get; set; } = 0f;

    private Node3D _visualRoot;
    private Node3D _modelRoot;
    private Marker3D _headAnchor;
    private AnimationPlayer _animPlayer;
    private CharacterCombatController _combatController;
    private Vector3 _desiredDirection = Vector3.Zero;
    private PawnVisualAction _desiredAction = PawnVisualAction.Idle;

    public bool IsCombatBusy => _combatController?.IsBusy == true;

    protected override void InitializeVisuals()
    {
        _visualRoot = GetNodeOrNull<Node3D>(VisualRootPath);
        _modelRoot = GetNodeOrNull<Node3D>(ModelRootPath);
        _headAnchor = GetNodeOrNull<Marker3D>(HeadAnchorPath);

        if (_modelRoot != null)
        {
            _modelRoot.Scale = ModelScale;
            _modelRoot.Position = ModelOffset;
        }

        _animPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath) ?? FindFirstChildOfType<AnimationPlayer>(_visualRoot);
        _combatController = CreateController();
        _combatController?.Initialize(this, _visualRoot, _animPlayer, CharacterDefinition);
    }

    protected override void UpdateVisualPresentation(double delta)
    {
        if (_modelRoot != null)
        {
            _modelRoot.Scale = ModelScale;
            _modelRoot.Position = ModelOffset;
        }

        _combatController?.Tick(delta, _desiredDirection, _desiredAction);
    }

    protected override void UpdateVisualAnimation(Vector3 direction, PawnVisualAction action)
    {
        _desiredDirection = direction;
        _desiredAction = action;
    }

    protected override Vector3 GetNameLabelWorldAnchor()
    {
        return _headAnchor?.GlobalPosition ?? (GlobalPosition + new Vector3(0f, 1.5f, 0f));
    }

    protected override void OnDied()
    {
        _combatController?.OnDeath();
    }

    public bool TryStartAttack(EnemyPawn target)
    {
        var weapon = Combat.DamageSystem.GetWeapon(this);
        return _combatController?.TryStartAttack(target, weapon) == true;
    }

    public void CancelCombatAction()
    {
        _combatController?.CancelAttack();
    }

    private CharacterCombatController CreateController()
    {
        if (CharacterDefinition == null)
            return null;

        return CharacterDefinition.CharacterId switch
        {
            "miyu" => new MiyuCombatController(),
            _ => new MiyuCombatController(),
        };
    }

    private static T FindFirstChildOfType<T>(Node node) where T : Node
    {
        if (node == null)
            return null;

        foreach (Node child in node.GetChildren())
        {
            if (child is T match)
                return match;

            T nested = FindFirstChildOfType<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
