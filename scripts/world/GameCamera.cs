using Godot;
using EndfieldZero.Core;

namespace EndfieldZero.World;

/// <summary>
/// Fixed top-down orthographic Camera3D. Looks straight down (-Y).
/// WASD/arrow key movement on XZ plane, mouse drag panning,
/// scroll wheel zoom (adjusts orthographic size).
/// Provides the camera position that WorldManager uses to determine chunk loading.
/// </summary>
public partial class GameCamera : Camera3D
{
    private const float MinCameraSizeEpsilon = 0.001f;

    /// <summary>Camera movement speed in units per second.</summary>
    [Export] public float MoveSpeed { get; set; } = 25f * Settings.BlockPixelSize;

    /// <summary>Zoom speed factor.</summary>
    [Export] public float ZoomSpeed { get; set; } = 0.1f;

    /// <summary>Minimum orthographic size (zoomed in).</summary>
    [Export] public float MinOrthoSize { get; set; } = 1.5f * Settings.BlockPixelSize;

    /// <summary>Maximum orthographic size (zoomed out).</summary>
    [Export] public float MaxOrthoSize { get; set; } = 625f * Settings.BlockPixelSize;

    /// <summary>Camera move speed boost when holding Shift.</summary>
    [Export] public float SprintMultiplier { get; set; } = 2.5f;

    /// <summary>Camera height above the XZ plane.</summary>
    [Export] public float CameraHeight { get; set; } = 32f * Settings.BlockPixelSize;

    /// <summary>Initial orthographic size.</summary>
    [Export] public float InitialOrthoSize { get; set; } = 93.75f * Settings.BlockPixelSize;

    private bool _isDragging;
    private float _currentOrthoSize;

    public override void _Ready()
    {
        SanitizeSettings();

        // Set up orthographic top-down view
        Projection = ProjectionType.Orthogonal;
        _currentOrthoSize = Mathf.Clamp(InitialOrthoSize, MinOrthoSize, MaxOrthoSize);
        Size = _currentOrthoSize;

        // Look straight down: rotate -90° around X axis
        RotationDegrees = new Vector3(-90f, 0f, 0f);

        // Position above origin
        Position = new Vector3(0f, CameraHeight, 0f);

        // Near/far planes
        Near = 0.1f;
        Far = CameraHeight * 2f;
    }

    private void SanitizeSettings()
    {
        MoveSpeed = Mathf.Max(MoveSpeed, 0f);
        ZoomSpeed = Mathf.Clamp(ZoomSpeed, 0f, 0.95f);
        SprintMultiplier = Mathf.Max(SprintMultiplier, 1f);
        CameraHeight = Mathf.Max(CameraHeight, MinCameraSizeEpsilon);
        MinOrthoSize = Mathf.Max(MinOrthoSize, MinCameraSizeEpsilon);
        MaxOrthoSize = Mathf.Max(MaxOrthoSize, MinOrthoSize);
        InitialOrthoSize = Mathf.Clamp(InitialOrthoSize, MinOrthoSize, MaxOrthoSize);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        HandleKeyboardMovement(dt);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        HandleMouseZoom(@event);
        HandleMouseDrag(@event);
    }

    private void HandleKeyboardMovement(float dt)
    {
        var direction = Vector3.Zero;

        // Movement on XZ plane (W = -Z, S = +Z, A = -X, D = +X)
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
            direction.Z -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
            direction.Z += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
            direction.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
            direction.X += 1;

        if (direction != Vector3.Zero)
        {
            float speed = MoveSpeed;
            if (Input.IsKeyPressed(Key.Shift))
                speed *= SprintMultiplier;

            // Scale movement by ortho size for consistent feel at all zoom levels
            float zoomFactor = _currentOrthoSize / InitialOrthoSize;
            var move = direction.Normalized() * speed * zoomFactor * dt;

            // Move on XZ, keep Y fixed at CameraHeight
            GlobalPosition = new Vector3(
                GlobalPosition.X + move.X,
                CameraHeight,
                GlobalPosition.Z + move.Z
            );
        }
    }

    private void HandleMouseZoom(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _currentOrthoSize *= (1f - ZoomSpeed);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _currentOrthoSize *= (1f + ZoomSpeed);
            }

            _currentOrthoSize = Mathf.Clamp(_currentOrthoSize, MinOrthoSize, MaxOrthoSize);
            Size = _currentOrthoSize;
        }
    }

    private void HandleMouseDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isDragging = mouseButton.Pressed;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            // Convert screen-space drag to world-space XZ movement
            // In orthographic, pixels map linearly to world units
            var viewport = GetViewport();
            Vector2 viewportSize = viewport.GetVisibleRect().Size;
            if (viewportSize.Y <= 0f)
                return;

            float pixelsToWorld = _currentOrthoSize * 2f / viewportSize.Y;

            float dx = -mouseMotion.Relative.X * pixelsToWorld;
            float dz = -mouseMotion.Relative.Y * pixelsToWorld;

            GlobalPosition = new Vector3(
                GlobalPosition.X + dx,
                CameraHeight,
                GlobalPosition.Z + dz
            );
        }
    }
}
