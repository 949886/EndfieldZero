using Cherry.Combat;
using Godot;

namespace Cherry.Pawn;

public abstract class CharacterCombatController
{
    protected Pawn3D Pawn { get; private set; }
    protected Node3D VisualRoot { get; private set; }
    protected AnimationPlayer AnimationPlayer { get; private set; }
    protected CharacterDefinition Definition { get; private set; }

    public bool IsBusy { get; protected set; }

    public virtual void Initialize(
        Pawn3D pawn,
        Node3D visualRoot,
        AnimationPlayer animationPlayer,
        CharacterDefinition definition)
    {
        Pawn = pawn;
        VisualRoot = visualRoot;
        AnimationPlayer = animationPlayer;
        Definition = definition;
    }

    public abstract void Tick(double delta, Vector3 desiredDirection, PawnVisualAction desiredAction);

    public abstract bool TryStartAttack(EnemyPawn target, WeaponDef weapon);

    public abstract void CancelAttack();

    public abstract void OnDeath();
}
