using Godot;

namespace EndfieldZero.Pawn;

public partial class Pawn3D : Pawn
{
    [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");
    [Export] public NodePath ModelRootPath { get; set; } = new("VisualRoot/Miyu");
    [Export] public NodePath HeadAnchorPath { get; set; } = new("VisualRoot/HeadAnchor");
    [Export] public Vector3 ModelScale { get; set; } = Vector3.One;
    [Export] public Vector3 ModelOffset { get; set; } = Vector3.Zero;
    [Export] public float ModelYawOffsetDegrees { get; set; } = 0f;
    [Export] public string IdleAnimation { get; set; } = "";
    [Export] public string MoveAnimation { get; set; } = "";
    [Export] public string DigAnimation { get; set; } = "";
    [Export] public string AttackAnimation { get; set; } = "";
    [Export] public string ShootAnimation { get; set; } = "";
    [Export] public string WateringAnimation { get; set; } = "";

    private Node3D _visualRoot;
    private Node3D _modelRoot;
    private Marker3D _headAnchor;
    private AnimationPlayer _animPlayer;
    private Pawn3DAnimController _animController;

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

        _animPlayer = FindFirstChildOfType<AnimationPlayer>(_visualRoot);
        _animController = new Pawn3DAnimController(
            _visualRoot,
            _animPlayer,
            ModelYawOffsetDegrees,
            IdleAnimation,
            MoveAnimation,
            DigAnimation,
            AttackAnimation,
            ShootAnimation,
            WateringAnimation);
    }

    protected override void UpdateVisualPresentation()
    {
        if (_modelRoot != null)
        {
            _modelRoot.Scale = ModelScale;
            _modelRoot.Position = ModelOffset;
        }
    }

    protected override void UpdateVisualAnimation(Vector3 direction, PawnVisualAction action)
    {
        _animController?.Update(direction, action);
    }

    protected override Vector3 GetNameLabelWorldAnchor()
    {
        return _headAnchor?.GlobalPosition ?? (GlobalPosition + new Vector3(0f, 1.5f, 0f));
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
