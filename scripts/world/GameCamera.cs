using EndfieldZero.Core;
using EndfieldZero.UI;
using Godot;

namespace EndfieldZero.World;

public enum CameraViewMode
{
    TopDown,
    Angled3D,
}

/// <summary>
/// Shared orthographic camera controller used for both the classic top-down view
/// and the fixed-angle 3D presentation mode.
/// </summary>
public partial class GameCamera : Camera3D
{
    private const float MinCameraSizeEpsilon = 0.001f;
    private const float RayPlaneEpsilon = 0.0001f;

    [Export] public float MoveSpeed { get; set; } = 25f;
    [Export] public float ZoomSpeed { get; set; } = 0.1f;
    [Export] public float MinOrthoSize { get; set; } = 1.5f;
    [Export] public float MaxOrthoSize { get; set; } = 625f;
    [Export] public float SprintMultiplier { get; set; } = 2.5f;
    [Export] public float CameraHeight { get; set; } = 32f;
    [Export] public float InitialOrthoSize { get; set; } = 93.75f;
    [Export] public float AngledPitchDegrees { get; set; } = -55f;
    [Export] public float AngledDistance { get; set; } = 26f;
    [Export] public float AngledHeight { get; set; } = 22f;
    [Export] public float TransitionDuration { get; set; } = 0.3f;
    [Export] public float OcclusionRadius { get; set; } = 0.65f;
    [Export] public float OcclusionAlpha { get; set; } = 0.22f;
    [Export] public float OcclusionScreenRadiusPixels { get; set; } = 110f;

    public static GameCamera Instance { get; private set; }
    public CameraViewMode ViewMode { get; private set; } = CameraViewMode.TopDown;
    public int YawIndex { get; private set; }
    public Vector2 FocusPointXZ { get; private set; }
    public float CurrentYawDegrees => ViewMode == CameraViewMode.Angled3D ? 45f + YawIndex * 90f : 0f;
    public Vector3 FocusWorldPosition => new(FocusPointXZ.X, 0f, FocusPointXZ.Y);

    private bool _isDragging;
    private float _currentOrthoSize;
    private Tween _transitionTween;

    public override void _Ready()
    {
        Instance = this;

        MoveSpeed *= Settings.BlockPixelSize;
        MinOrthoSize *= Settings.BlockPixelSize;
        MaxOrthoSize *= Settings.BlockPixelSize;
        CameraHeight *= Settings.BlockPixelSize;
        InitialOrthoSize *= Settings.BlockPixelSize;
        AngledDistance *= Settings.BlockPixelSize;
        AngledHeight *= Settings.BlockPixelSize;
        OcclusionRadius *= Settings.BlockPixelSize;

        SanitizeSettings();

        Projection = ProjectionType.Orthogonal;
        _currentOrthoSize = Mathf.Clamp(InitialOrthoSize, MinOrthoSize, MaxOrthoSize);
        Size = _currentOrthoSize;
        Near = 0.1f;

        FocusPointXZ = new Vector2(Position.X, Position.Z);
        ApplyCameraStateImmediate();
        UpdateOcclusionState();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        HandleKeyboardMovement(dt);

        if (!IsTransitioning())
            ApplyCameraStateImmediate();

        UpdateOcclusionState();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Tab)
            {
                ToggleViewMode();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (ViewMode == CameraViewMode.Angled3D && key.AltPressed)
            {
                if (key.Keycode == Key.Q)
                {
                    RotateYaw(-1);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (key.Keycode == Key.E)
                {
                    RotateYaw(1);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        HandleMouseZoom(@event);
        HandleMouseDrag(@event);
    }

    public void ToggleViewMode()
    {
        SetViewMode(ViewMode == CameraViewMode.TopDown
            ? CameraViewMode.Angled3D
            : CameraViewMode.TopDown);
    }

    public void SetViewMode(CameraViewMode mode)
    {
        if (ViewMode == mode)
            return;

        ViewMode = mode;
        WorldManager.Instance?.RefreshViewDependentVisuals();
        AnimateCameraState();
    }

    public void RotateYaw(int direction)
    {
        if (ViewMode != CameraViewMode.Angled3D || direction == 0)
            return;

        YawIndex = Mathf.PosMod(YawIndex + direction, 4);
        AnimateCameraState();
    }

    public bool TryGetSelectedPawnOcclusionAnchor(out Vector3 anchor)
    {
        if (SelectionManager.Instance?.Selected.Count > 0 && SelectionManager.Instance.Selected[0] is Node3D selectedPawn)
        {
            anchor = selectedPawn.GlobalPosition;
            return true;
        }

        anchor = Vector3.Zero;
        return false;
    }

    public bool TryGetSelectedPawnScreenPosition(out Vector2 screenPos)
    {
        if (TryGetSelectedPawnOcclusionAnchor(out Vector3 anchor))
        {
            screenPos = UnprojectPosition(anchor);
            return true;
        }

        screenPos = Vector2.Zero;
        return false;
    }

    public Vector2 GetOcclusionMouseScreenPosition()
    {
        return GetViewport()?.GetMousePosition() ?? Vector2.Zero;
    }

    public Vector2 GetScreenMotion(Vector3 worldDirection)
    {
        Vector3 sampleOrigin = FocusWorldPosition;
        Vector2 origin = UnprojectPosition(sampleOrigin);
        Vector2 xDelta = UnprojectPosition(sampleOrigin + Vector3.Right) - origin;
        Vector2 zDelta = UnprojectPosition(sampleOrigin + Vector3.Back) - origin;
        return xDelta * worldDirection.X + zDelta * worldDirection.Z;
    }

    public Vector3 GetScreenRightDirection()
    {
        Vector2 right = GetAxisFromScreen(Vector2.Right);
        return new Vector3(right.X, 0f, right.Y);
    }

    public Vector3 GetScreenUpDirection()
    {
        Vector2 up = GetAxisFromScreen(Vector2.Up);
        return new Vector3(up.X, 0f, up.Y);
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
        AngledDistance = Mathf.Max(AngledDistance, Settings.BlockPixelSize);
        AngledHeight = Mathf.Max(AngledHeight, Settings.BlockPixelSize);
        TransitionDuration = Mathf.Max(TransitionDuration, 0.01f);
        OcclusionScreenRadiusPixels = Mathf.Max(OcclusionScreenRadiusPixels, 1f);
    }

    private void HandleKeyboardMovement(float dt)
    {
        if (IsTransitioning())
            return;

        Vector3 direction = Vector3.Zero;
        Vector3 screenUp = GetScreenUpDirection();
        Vector3 screenRight = GetScreenRightDirection();

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
            direction += screenUp;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
            direction -= screenUp;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
            direction -= screenRight;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
            direction += screenRight;

        direction.Y = 0f;
        if (direction == Vector3.Zero)
            return;

        float speed = MoveSpeed;
        if (Input.IsKeyPressed(Key.Shift))
            speed *= SprintMultiplier;

        float zoomFactor = _currentOrthoSize / InitialOrthoSize;
        Vector3 move = direction.Normalized() * speed * zoomFactor * dt;
        FocusPointXZ += new Vector2(move.X, move.Z);
    }

    private void HandleMouseZoom(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            _currentOrthoSize *= 1f - ZoomSpeed;
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            _currentOrthoSize *= 1f + ZoomSpeed;
        else
            return;

        _currentOrthoSize = Mathf.Clamp(_currentOrthoSize, MinOrthoSize, MaxOrthoSize);
        Size = _currentOrthoSize;
    }

    private void HandleMouseDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
                _isDragging = mouseButton.Pressed;
            return;
        }

        if (@event is not InputEventMouseMotion mouseMotion || !_isDragging || IsTransitioning())
            return;

        Vector2 previousMouse = mouseMotion.Position - mouseMotion.Relative;
        Vector3 prevWorld = ProjectToPlane(previousMouse, 0f);
        Vector3 currentWorld = ProjectToPlane(mouseMotion.Position, 0f);
        Vector3 delta = prevWorld - currentWorld;

        FocusPointXZ += new Vector2(delta.X, delta.Z);
    }

    private void ApplyCameraStateImmediate()
    {
        var state = BuildTargetState();
        GlobalPosition = state.Position;
        RotationDegrees = state.RotationDegrees;
        Size = _currentOrthoSize;
        Far = state.FarPlane;
    }

    private void AnimateCameraState()
    {
        _transitionTween?.Kill();
        var state = BuildTargetState();
        _transitionTween = CreateTween();
        _transitionTween.SetTrans(Tween.TransitionType.Cubic);
        _transitionTween.SetEase(Tween.EaseType.Out);
        _transitionTween.TweenProperty(this, "global_position", state.Position, TransitionDuration);
        _transitionTween.Parallel().TweenProperty(this, "rotation_degrees", state.RotationDegrees, TransitionDuration);
        _transitionTween.Parallel().TweenProperty(this, "far", state.FarPlane, TransitionDuration);
    }

    private bool IsTransitioning()
    {
        return _transitionTween != null && _transitionTween.IsRunning();
    }

    private CameraState BuildTargetState()
    {
        Size = _currentOrthoSize;

        if (ViewMode == CameraViewMode.TopDown)
        {
            return new CameraState(
                new Vector3(FocusPointXZ.X, CameraHeight, FocusPointXZ.Y),
                new Vector3(-90f, 0f, 0f),
                Mathf.Max(CameraHeight * 2f, _currentOrthoSize * 4f));
        }

        float yaw = 45f + YawIndex * 90f;
        float pitchRadians = Mathf.DegToRad(AngledPitchDegrees);
        float pitchMagnitude = Mathf.Clamp(Mathf.Abs(pitchRadians), Mathf.DegToRad(5f), Mathf.DegToRad(85f));
        float groundDepthHalfSpan = (_currentOrthoSize * 0.5f) / Mathf.Tan(pitchMagnitude);
        float baseFocusDistance = AngledDistance * Mathf.Cos(pitchRadians) - AngledHeight * Mathf.Sin(pitchRadians);
        float focusDistance = Mathf.Max(baseFocusDistance, Near + groundDepthHalfSpan + Settings.BlockPixelSize * 2f);

        Vector3 forward = GetForwardDirection(yaw, pitchRadians);
        Vector3 position = FocusWorldPosition - forward * focusDistance;
        float verticalBuffer = Mathf.Max(CameraHeight, Settings.MaxLayers * Settings.BlockPixelSize * 4f);
        float farPlane = Mathf.Max(focusDistance + groundDepthHalfSpan + verticalBuffer, 256f);
        return new CameraState(position, new Vector3(AngledPitchDegrees, yaw, 0f), farPlane);
    }

    private Vector2 GetAxisFromScreen(Vector2 desiredScreenDir)
    {
        Vector3 sampleOrigin = FocusWorldPosition;
        Vector2 origin = UnprojectPosition(sampleOrigin);
        Vector2 xDelta = UnprojectPosition(sampleOrigin + Vector3.Right) - origin;
        Vector2 zDelta = UnprojectPosition(sampleOrigin + Vector3.Back) - origin;

        float det = xDelta.X * zDelta.Y - xDelta.Y * zDelta.X;
        if (Mathf.Abs(det) < 0.0001f)
        {
            return desiredScreenDir == Vector2.Up
                ? new Vector2(0f, -1f)
                : new Vector2(1f, 0f);
        }

        float vx = (desiredScreenDir.X * zDelta.Y - desiredScreenDir.Y * zDelta.X) / det;
        float vz = (xDelta.X * desiredScreenDir.Y - xDelta.Y * desiredScreenDir.X) / det;
        return new Vector2(vx, vz).Normalized();
    }

    private void UpdateOcclusionState()
    {
        var viewport = GetViewport();
        Vector2 screenSize = viewport?.GetVisibleRect().Size ?? Vector2.One;
        Vector2 mouseScreen = GetOcclusionMouseScreenPosition();
        Vector2 mouseScreenUv = new(
            screenSize.X > 0.0001f ? mouseScreen.X / screenSize.X : 0.5f,
            screenSize.Y > 0.0001f ? mouseScreen.Y / screenSize.Y : 0.5f);
        bool hasSelectedPawn = TryGetSelectedPawnScreenPosition(out Vector2 selectedScreen);
        Vector2 selectedScreenUv = hasSelectedPawn
            ? new Vector2(
                screenSize.X > 0.0001f ? selectedScreen.X / screenSize.X : 0.5f,
                screenSize.Y > 0.0001f ? selectedScreen.Y / screenSize.Y : 0.5f)
            : new Vector2(-10f, -10f);
        Vector2 occlusionRadiusUv = new(
            screenSize.X > 0.0001f ? OcclusionScreenRadiusPixels / screenSize.X : 0.1f,
            screenSize.Y > 0.0001f ? OcclusionScreenRadiusPixels / screenSize.Y : 0.1f);

        ChunkRenderer.UpdateSharedViewState(
            mouseScreenUv,
            selectedScreenUv,
            occlusionRadiusUv,
            ViewMode == CameraViewMode.Angled3D,
            hasSelectedPawn,
            OcclusionAlpha);
    }

    private static Vector3 GetPlanarForward(float yawDegrees)
    {
        float radians = Mathf.DegToRad(yawDegrees);
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)).Normalized();
    }

    private static Vector3 GetForwardDirection(float yawDegrees, float pitchRadians)
    {
        Vector3 planarForward = GetPlanarForward(yawDegrees);
        float planarScale = Mathf.Cos(pitchRadians);
        return new Vector3(
            planarForward.X * planarScale,
            Mathf.Sin(pitchRadians),
            planarForward.Z * planarScale).Normalized();
    }

    private Vector3 ProjectToPlane(Vector2 screenPos, float planeY)
    {
        Vector3 from = ProjectRayOrigin(screenPos);
        Vector3 dir = ProjectRayNormal(screenPos);

        if (Mathf.Abs(dir.Y) < RayPlaneEpsilon)
            return new Vector3(from.X, planeY, from.Z);

        float t = (planeY - from.Y) / dir.Y;
        return from + dir * t;
    }

    private readonly record struct CameraState(Vector3 Position, Vector3 RotationDegrees, float FarPlane);
}
