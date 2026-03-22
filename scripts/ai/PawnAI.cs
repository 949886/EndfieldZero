using System;
using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using Godot;

namespace EndfieldZero.AI;

/// <summary>
/// Attention-mechanism AI controller for a single pawn.
///
/// Inspired by Transformer self-attention:
///   1. Each candidate action produces a Query vector (Q) — what it "wants"
///   2. The current context produces a Key vector (K) — what's happening
///   3. Score = softmax(Q · K / √d)
///   4. The highest-scoring action is selected for execution
///
/// Re-evaluates every AIEvalInterval ticks unless current action completes.
/// </summary>
public class PawnAI
{
    private readonly Pawn.Pawn _pawn;
    private readonly List<AIAction> _actions = new();
    private AIAction _currentAction;
    private long _lastEvalTick;

    /// <summary>Current context snapshot.</summary>
    private readonly AIContext _context = new();

    public PawnAI(Pawn.Pawn pawn)
    {
        _pawn = pawn;

        // Register all available actions
        RegisterAction(new Actions.WanderAction());
        RegisterAction(new Actions.SatisfyNeedAction());
        RegisterAction(new Actions.DoJobAction());
        RegisterAction(new Actions.HaulAction());

        // Subscribe to tick
        EventBus.Tick += OnTick;
    }

    public void Dispose()
    {
        EventBus.Tick -= OnTick;
    }

    /// <summary>Current active action name (for debug).</summary>
    public string CurrentActionName => _currentAction?.Name ?? "None";

    public void RegisterAction(AIAction action)
    {
        action.Owner = _pawn;
        _actions.Add(action);
    }

    private void OnTick(long tick)
    {
        if (!_pawn.IsAlive) return;

        // Skip AI when player is controlling this pawn
        if (_pawn.IsPlayerControlled) return;

        // Update context
        _context.Pawn = _pawn;
        _context.CurrentTick = tick;
        _context.JobAvailability = (JobSystem.Instance?.HasAvailableJobs(_pawn) ?? false) ? 1f : 0f;

        // Execute current action every tick
        if (_currentAction != null && _currentAction.IsRunning)
        {
            _currentAction.Execute(_context);

            // Check if completed
            if (_currentAction.IsComplete(_context))
            {
                _currentAction.OnStop();
                _currentAction = null;
            }
        }

        // Re-evaluate periodically or when idle
        bool shouldEval = (tick - _lastEvalTick) >= Settings.AIEvalInterval
                       || _currentAction == null;

        if (shouldEval)
        {
            _lastEvalTick = tick;
            Evaluate();
        }
    }

    /// <summary>
    /// Attention-based action selection:
    ///   score_i = Q_i · K / √d
    ///   select = argmax(softmax(scores))
    /// </summary>
    private void Evaluate()
    {
        // If current action is running and doesn't want to be interrupted, skip
        if (_currentAction != null && _currentAction.IsRunning
            && !_currentAction.ShouldInterrupt(_context))
        {
            return;
        }

        float[] key = _context.GetKeyVector();
        float sqrtD = MathF.Sqrt(AIContext.Dimensions);

        AIAction bestAction = null;
        float bestScore = float.MinValue;

        foreach (var action in _actions)
        {
            if (!action.CanExecute(_context))
                continue;

            float[] query = action.GetQueryVector(_context);
            float rawScore = DotProduct(query, key) / sqrtD;

            if (rawScore > bestScore)
            {
                bestScore = rawScore;
                bestAction = action;
            }
        }

        // Switch action if a better one is found
        if (bestAction != null && bestAction != _currentAction)
        {
            _currentAction?.OnStop();
            _currentAction = bestAction;
            _currentAction.OnStart(_context);
        }
    }

    /// <summary>Full attention evaluation with softmax (for debugging / future use).</summary>
    public float[] GetActionProbabilities()
    {
        float[] key = _context.GetKeyVector();
        float sqrtD = MathF.Sqrt(AIContext.Dimensions);

        var scores = new List<float>();
        foreach (var action in _actions)
        {
            if (!action.CanExecute(_context))
            {
                scores.Add(float.MinValue);
                continue;
            }
            float[] q = action.GetQueryVector(_context);
            scores.Add(DotProduct(q, key) / sqrtD);
        }

        return Softmax(scores.ToArray());
    }

    // --- Math helpers ---

    private static float DotProduct(float[] a, float[] b)
    {
        float sum = 0f;
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
            sum += a[i] * b[i];
        return sum;
    }

    private static float[] Softmax(float[] scores)
    {
        float max = float.MinValue;
        foreach (float s in scores)
            if (s > max) max = s;

        float sumExp = 0f;
        var result = new float[scores.Length];
        for (int i = 0; i < scores.Length; i++)
        {
            result[i] = MathF.Exp(scores[i] - max);
            sumExp += result[i];
        }

        if (sumExp > 0f)
            for (int i = 0; i < result.Length; i++)
                result[i] /= sumExp;

        return result;
    }
}
