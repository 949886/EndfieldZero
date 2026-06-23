using EndfieldZero.Core;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Pawn;

internal static class PawnNameLabel3D
{
    private const float BasePixelSize = 0.05f;
    private const int FontSize = 20;
    private const float ReferenceOrthoSizeFallback = 93.75f;
    private const float PlaneEpsilon = 0.0001f;
    private static readonly Vector2 ScreenOffsetPixels = new(0f, 16f);
    private static readonly Vector2 ShadowOffsetPixels = new(1.5f, 1.5f);

    public static Label3D Create(string text, bool shadow = false)
    {
        return new Label3D
        {
            Text = text ?? string.Empty,
            PixelSize = BasePixelSize,
            FontSize = FontSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = shadow
                ? new Color(0f, 0f, 0f, 0.88f)
                : new Color(0.95f, 0.98f, 1f, 0.95f),
            OutlineSize = 0,
            NoDepthTest = true,
            RenderPriority = shadow ? 9 : 10,
        };
    }

    public static void Update(
        Label3D label,
        Label3D shadowLabel,
        Camera3D camera,
        Vector3 anchorWorldPosition,
        string text,
        bool visible)
    {
        if (label == null || shadowLabel == null)
            return;

        label.Text = text ?? string.Empty;
        shadowLabel.Text = label.Text;

        if (!visible || camera == null || string.IsNullOrWhiteSpace(label.Text))
        {
            label.Visible = false;
            shadowLabel.Visible = false;
            return;
        }

        Vector2 anchorScreen = camera.UnprojectPosition(anchorWorldPosition);
        if (!IsFinite(anchorScreen))
        {
            label.Visible = false;
            shadowLabel.Visible = false;
            return;
        }

        Vector2 targetScreen = anchorScreen + ScreenOffsetPixels;
        if (!TryProjectScreenPointToAnchorPlane(camera, anchorWorldPosition, anchorScreen, targetScreen, out Vector3 worldPosition))
        {
            label.Visible = false;
            shadowLabel.Visible = false;
            return;
        }

        Vector2 shadowScreen = targetScreen + ShadowOffsetPixels;
        if (!TryProjectScreenPointToAnchorPlane(camera, anchorWorldPosition, anchorScreen, shadowScreen, out Vector3 shadowWorldPosition))
        {
            label.Visible = false;
            shadowLabel.Visible = false;
            return;
        }

        float compensatedPixelSize = GetCompensatedPixelSize(camera, anchorWorldPosition);
        label.PixelSize = compensatedPixelSize;
        shadowLabel.PixelSize = compensatedPixelSize;
        label.GlobalPosition = worldPosition;
        shadowLabel.GlobalPosition = shadowWorldPosition;
        label.Visible = true;
        shadowLabel.Visible = true;
    }

    private static float GetReferenceOrthoSize(Camera3D camera)
    {
        float fallback = ReferenceOrthoSizeFallback * Settings.BlockPixelSize;

        if (camera is GameCamera gameCamera)
            return Mathf.Max(gameCamera.InitialOrthoSize, PlaneEpsilon);

        return Mathf.Max(GameCamera.Instance?.InitialOrthoSize ?? fallback, PlaneEpsilon);
    }

    private static float GetCompensatedPixelSize(Camera3D camera, Vector3 anchorWorldPosition)
    {
        if (camera.Projection != Camera3D.ProjectionType.Perspective)
        {
            float referenceOrthoSize = GetReferenceOrthoSize(camera);
            return BasePixelSize * (camera.Size / referenceOrthoSize);
        }

        float distance = camera.GlobalPosition.DistanceTo(anchorWorldPosition);
        float fovFactor = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);

        if (camera is GameCamera gameCamera)
        {
            float referenceDistance = Mathf.Max(gameCamera.GetPerspectiveReferenceDistance(), PlaneEpsilon);
            float referenceFovFactor = Mathf.Tan(Mathf.DegToRad(gameCamera.PerspectiveFovDegrees) * 0.5f);
            return BasePixelSize * ((distance * fovFactor) / Mathf.Max(referenceDistance * referenceFovFactor, PlaneEpsilon));
        }

        return BasePixelSize * Mathf.Max(distance * fovFactor, PlaneEpsilon);
    }

    private static bool TryProjectScreenPointToAnchorPlane(
        Camera3D camera,
        Vector3 anchorWorldPosition,
        Vector2 anchorScreen,
        Vector2 targetScreen,
        out Vector3 worldPosition)
    {
        worldPosition = anchorWorldPosition;

        Vector3 planeNormal = camera.ProjectRayNormal(anchorScreen).Normalized();
        Vector3 rayOrigin = camera.ProjectRayOrigin(targetScreen);
        Vector3 rayDirection = camera.ProjectRayNormal(targetScreen).Normalized();

        float denominator = planeNormal.Dot(rayDirection);
        if (Mathf.Abs(denominator) < PlaneEpsilon)
            return false;

        float distance = planeNormal.Dot(anchorWorldPosition - rayOrigin) / denominator;
        worldPosition = rayOrigin + rayDirection * distance;
        return IsFinite(worldPosition);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
