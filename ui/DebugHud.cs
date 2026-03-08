using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Debug overlay showing camera position, zoom level, and FPS.
/// Displayed in the top-left corner during development.
/// </summary>
public partial class DebugHud : Label
{
    private int _frameCount;
    private double _fpsTimer;
    private float _currentFps;

    public override void _Ready()
    {
        // Styling
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeFontSizeOverride("font_size", 14);

        // Position at top-left with some margin
        Position = new Vector2(10, 10);
    }

    public override void _Process(double delta)
    {
        // FPS counter
        _frameCount++;
        _fpsTimer += delta;
        if (_fpsTimer >= 0.5)
        {
            _currentFps = (float)(_frameCount / _fpsTimer);
            _frameCount = 0;
            _fpsTimer = 0;
        }

        // Camera info (Camera3D)
        var camera = GetViewport()?.GetCamera3D();
        string cameraPos = camera != null
            ? $"({camera.GlobalPosition.X:F0}, {camera.GlobalPosition.Z:F0})"
            : "(N/A)";
        string cameraZoom = camera != null
            ? $"{camera.Size:F0}"
            : "N/A";

        Text = $"FPS: {_currentFps:F0}\nCamera XZ: {cameraPos}\nOrtho Size: {cameraZoom}";
    }
}
