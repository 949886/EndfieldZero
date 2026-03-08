using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Fixed top-down camera with WASD/arrow key movement, mouse drag panning,
/// and scroll wheel zoom. Provides the camera position that WorldManager
/// uses to determine which chunks to load.
/// </summary>
public partial class GameCamera : Camera2D
{
    /// <summary>Camera movement speed in pixels per second.</summary>
    [Export] public float MoveSpeed { get; set; } = 800f;

    /// <summary>Zoom speed factor.</summary>
    [Export] public float ZoomSpeed { get; set; } = 0.1f;

    /// <summary>Minimum zoom level (zoomed in).</summary>
    [Export] public float MinZoom { get; set; } = 0.2f;

    /// <summary>Maximum zoom level (zoomed out).</summary>
    [Export] public float MaxZoom { get; set; } = 5.0f;

    /// <summary>Camera move speed boost when holding Shift.</summary>
    [Export] public float SprintMultiplier { get; set; } = 2.5f;

    private bool _isDragging;
    private Vector2 _dragStart;

    public override void _Ready()
    {
        // Start at world origin, zoomed out a bit
        Zoom = Vector2.One * 0.5f;
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
        HandleEdgeScroll(@event);
    }

    private void HandleKeyboardMovement(float dt)
    {
        var direction = Vector2.Zero;

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
            direction.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
            direction.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
            direction.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
            direction.X += 1;

        if (direction != Vector2.Zero)
        {
            float speed = MoveSpeed;
            if (Input.IsKeyPressed(Key.Shift))
                speed *= SprintMultiplier;

            // Scale movement by zoom so it feels consistent at all zoom levels
            float zoomFactor = 1f / Zoom.X;
            GlobalPosition += direction.Normalized() * speed * zoomFactor * dt;
        }
    }

    private void HandleMouseZoom(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                float currentZoom = Zoom.X;

                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    currentZoom *= (1f + ZoomSpeed);
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    currentZoom *= (1f - ZoomSpeed);
                }

                currentZoom = Mathf.Clamp(currentZoom, MinZoom, MaxZoom);
                Zoom = Vector2.One * currentZoom;
            }
        }
    }

    private void HandleMouseDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                if (mouseButton.Pressed)
                {
                    _isDragging = true;
                    _dragStart = mouseButton.GlobalPosition;
                }
                else
                {
                    _isDragging = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            float zoomFactor = 1f / Zoom.X;
            GlobalPosition -= mouseMotion.Relative * zoomFactor;
        }
    }

    private void HandleEdgeScroll(InputEvent @event)
    {
        // Edge scrolling can be added here if desired
    }
}
