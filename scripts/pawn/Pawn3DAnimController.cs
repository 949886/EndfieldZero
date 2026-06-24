using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EndfieldZero.Pawn;

public class Pawn3DAnimController
{
    private readonly Node3D _visualRoot;
    private readonly AnimationPlayer _animPlayer;
    private readonly Dictionary<PawnVisualAction, string> _explicitAnimations;
    private readonly HashSet<PawnVisualAction> _warnedMissing = new();
    private readonly float _yawOffsetRadians;

    private string _currentAnim = "";
    private Vector3 _lastFacing = Vector3.Forward;
    private Dictionary<PawnVisualAction, string> _resolvedAnimations;

    public Pawn3DAnimController(
        Node3D visualRoot,
        AnimationPlayer animPlayer,
        float yawOffsetDegrees,
        string idleAnimation,
        string moveAnimation,
        string digAnimation,
        string attackAnimation,
        string shootAnimation,
        string wateringAnimation)
    {
        _visualRoot = visualRoot;
        _animPlayer = animPlayer;
        _yawOffsetRadians = Mathf.DegToRad(yawOffsetDegrees);
        _explicitAnimations = new Dictionary<PawnVisualAction, string>
        {
            [PawnVisualAction.Idle] = idleAnimation,
            [PawnVisualAction.Move] = moveAnimation,
            [PawnVisualAction.Dig] = digAnimation,
            [PawnVisualAction.Attack] = attackAnimation,
            [PawnVisualAction.Shoot] = shootAnimation,
            [PawnVisualAction.Watering] = wateringAnimation,
        };
    }

    public void Update(Vector3 direction, PawnVisualAction action)
    {
        if (direction.LengthSquared() > 0.01f)
            _lastFacing = direction.Normalized();

        RotateVisual();

        string targetAnimation = ResolveWithFallback(action);
        if (string.IsNullOrEmpty(targetAnimation) || _animPlayer == null || targetAnimation == _currentAnim)
            return;

        _currentAnim = targetAnimation;
        _animPlayer.Play(targetAnimation);
    }

    private void RotateVisual()
    {
        if (_visualRoot == null)
            return;

        Vector3 facing = _lastFacing;
        facing.Y = 0f;
        if (facing.LengthSquared() <= 0.0001f)
            return;

        float yaw = Mathf.Atan2(facing.X, facing.Z) + _yawOffsetRadians;
        Vector3 rotation = _visualRoot.Rotation;
        rotation.Y = yaw;
        _visualRoot.Rotation = rotation;
    }

    private string ResolveWithFallback(PawnVisualAction action)
    {
        EnsureResolvedAnimations();

        foreach (PawnVisualAction fallback in GetFallbackOrder(action))
        {
            if (_resolvedAnimations.TryGetValue(fallback, out string animationName) && !string.IsNullOrEmpty(animationName))
                return animationName;
        }

        if (_warnedMissing.Add(action))
            GD.PrintErr($"[Pawn3DAnimController] No animation found for {action}.");

        return string.Empty;
    }

    private void EnsureResolvedAnimations()
    {
        if (_resolvedAnimations != null)
            return;

        _resolvedAnimations = new Dictionary<PawnVisualAction, string>();
        string[] availableAnimations = _animPlayer?.GetAnimationList() ?? System.Array.Empty<string>();

        foreach (PawnVisualAction action in System.Enum.GetValues(typeof(PawnVisualAction)))
        {
            string configured = _explicitAnimations.GetValueOrDefault(action);
            if (!string.IsNullOrWhiteSpace(configured) && availableAnimations.Contains(configured))
            {
                _resolvedAnimations[action] = configured;
                continue;
            }

            _resolvedAnimations[action] = FindBestMatch(availableAnimations, GetKeywords(action));
        }
    }

    private static IEnumerable<PawnVisualAction> GetFallbackOrder(PawnVisualAction action)
    {
        yield return action;

        switch (action)
        {
            case PawnVisualAction.Watering:
                yield return PawnVisualAction.Dig;
                break;
            case PawnVisualAction.Shoot:
                yield return PawnVisualAction.Attack;
                break;
            case PawnVisualAction.Attack:
                yield return PawnVisualAction.Shoot;
                break;
        }

        yield return PawnVisualAction.Idle;
    }

    private static string[] GetKeywords(PawnVisualAction action)
    {
        return action switch
        {
            PawnVisualAction.Move => new[] { "move", "walk", "run", "jog", "locomotion" },
            PawnVisualAction.Dig => new[] { "dig", "mine", "work", "pick", "harvest", "gather", "chop" },
            PawnVisualAction.Attack => new[] { "attack", "melee", "hit", "slash", "punch" },
            PawnVisualAction.Shoot => new[] { "shoot", "shot", "fire", "aim", "gun" },
            PawnVisualAction.Watering => new[] { "water", "watering", "pour", "sprinkle" },
            _ => new[] { "idle", "stand", "wait", "loop" },
        };
    }

    private static string FindBestMatch(IEnumerable<string> animationNames, IEnumerable<string> keywords)
    {
        string[] keywordArray = keywords.Select(Normalize).ToArray();

        foreach (string animationName in animationNames)
        {
            string normalizedName = Normalize(animationName);
            if (keywordArray.Any(keyword => normalizedName.Contains(keyword)))
                return animationName;
        }

        return string.Empty;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] filtered = value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(filtered);
    }
}
