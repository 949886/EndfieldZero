using Cherry.Pawn;
using Godot;

namespace Cherry.Combat;

public partial class CombatVfxManager : Node3D
{
    public static CombatVfxManager Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public static CombatVfxManager Resolve(Node context)
    {
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
            return Instance;

        var tree = context?.GetTree();
        var currentScene = tree?.CurrentScene;
        if (currentScene == null)
            return null;

        var existing = currentScene.GetNodeOrNull<CombatVfxManager>("CombatVfxManager");
        if (existing != null)
            return existing;

        var manager = new CombatVfxManager { Name = "CombatVfxManager" };
        Node parent = currentScene.GetNodeOrNull<Node>("EntityContainer") ?? currentScene;
        parent.AddChild(manager);
        return manager;
    }

    public void SpawnMuzzleFlash(CharacterDefinition definition, Vector3 position)
    {
        SpawnBurst(definition?.MuzzleFlashScene, position, definition?.MuzzleFlashColor ?? new Color(1f, 0.85f, 0.55f, 1f));
    }

    public void SpawnImpact(CharacterDefinition definition, bool isHit, Vector3 position)
    {
        PackedScene scene = isHit ? definition?.HitImpactScene : definition?.MissImpactScene;
        Color tint = isHit
            ? definition?.HitImpactColor ?? new Color(1f, 0.55f, 0.2f, 1f)
            : definition?.MissImpactColor ?? new Color(0.75f, 0.85f, 1f, 1f);
        SpawnBurst(scene, position, tint);
    }

    public void SpawnProjectile(CharacterDefinition definition, PreparedRangedShot shot, Vector3 origin)
    {
        CombatProjectile projectile = definition?.ProjectileScene != null
            ? definition.ProjectileScene.Instantiate<CombatProjectile>()
            : new CombatProjectile();
        projectile.Initialize(shot, definition, this, origin);
        AddChild(projectile);
    }

    private void SpawnBurst(PackedScene scene, Vector3 position, Color tint)
    {
        Node3D effect = scene != null
            ? scene.Instantiate<Node3D>()
            : new CombatBurstVfx();
        AddChild(effect);
        effect.GlobalPosition = position;
        if (effect is CombatBurstVfx burst)
            burst.ApplyTint(tint);
    }
}
