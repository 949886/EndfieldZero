using EndfieldZero.Jobs;
using EndfieldZero.Managers;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Debug overlay: FPS, game time, camera, colonists, selection, AI, jobs, tool mode.
/// </summary>
public partial class DebugHud : Label
{
    private int _frameCount;
    private double _fpsTimer;
    private float _currentFps;

    public override void _Ready()
    {
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeFontSizeOverride("font_size", 14);
    }

    public override void _Process(double delta)
    {
        _frameCount++;
        _fpsTimer += delta;
        if (_fpsTimer >= 0.5)
        {
            _currentFps = (float)(_frameCount / _fpsTimer);
            _frameCount = 0;
            _fpsTimer = 0;
        }

        var camera = GetViewport()?.GetCamera3D();
        string cameraPos = camera != null
            ? $"({camera.GlobalPosition.X:F0}, {camera.GlobalPosition.Z:F0})"
            : "(N/A)";

        var time = Core.TimeManager.Instance;
        string timeStr = time != null
            ? $"Day {time.CurrentDay} {time.CurrentHour:D2}:00 ({time.GameSpeed}×)"
            : "N/A";

        // Pawn counts + selected AI
        int pawnCount = 0;
        string aiInfo = "";
        if (PawnManager.Instance != null)
        {
            foreach (var pawn in PawnManager.Instance.GetAllPawns())
            {
                pawnCount++;
                if (pawn.IsSelected && pawn.AI != null)
                    aiInfo = $" AI:{pawn.AI.CurrentActionName}";
            }
        }

        // Selection
        var sel = GetParent()?.GetNodeOrNull<SelectionManager>("SelectionManager");
        int selCount = sel?.Selected.Count ?? 0;

        // Jobs
        int availJobs = JobSystem.Instance?.CountByStatus(JobStatus.Available) ?? 0;
        int activeJobs = JobSystem.Instance?.CountByStatus(JobStatus.InProgress) ?? 0;
        int reservedJobs = JobSystem.Instance?.CountByStatus(JobStatus.Reserved) ?? 0;

        // Tool mode
        string toolStr = ToolModeManager.Instance?.CurrentMode.ToString() ?? "Select";

        Text = $"FPS: {_currentFps:F0} | {timeStr} | Tool: {toolStr}\n" +
               $"Camera: {cameraPos} | Colonists: {pawnCount}\n" +
               $"Selected: {selCount}{aiInfo} | Jobs: {availJobs}待/{reservedJobs}领/{activeJobs}做";
    }
}
