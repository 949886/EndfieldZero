using EndfieldZero.Managers;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Debug overlay showing FPS, game time, camera position, and pawn count.
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

        int pawnCount = 0;
        if (PawnManager.Instance != null)
        {
            foreach (var _ in PawnManager.Instance.GetAllPawns())
                pawnCount++;
        }

        Text = $"FPS: {_currentFps:F0} | {timeStr}\nCamera: {cameraPos}\nColonists: {pawnCount}";
    }
}
