using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.Jobs;
using Godot;

namespace EndfieldZero.UI;

/// <summary>
/// Renders job designation overlays directly on the 3D world.
/// Shows colored markers on blocks that have been designated for jobs.
/// Uses a MeshInstance3D with a shader to render semi-transparent quads.
///
/// Colors:
///   Mine    — red outline/flash
///   Construct — blue
///   Grow    — green
///   Reserved/InProgress — yellow flash
/// </summary>
public partial class JobOverlay : MeshInstance3D
{
    private static ShaderMaterial _overlayMaterial;

    private static readonly Dictionary<string, Color> JobTypeColors = new()
    {
        { "Mine", new Color(1f, 0.3f, 0.2f, 0.4f) },
        { "Construct", new Color(0.3f, 0.5f, 1f, 0.4f) },
        { "Grow", new Color(0.3f, 0.9f, 0.3f, 0.4f) },
        { "Haul", new Color(0.9f, 0.7f, 0.2f, 0.4f) },
        { "Harvest", new Color(0.9f, 0.9f, 0.2f, 0.4f) },
    };

    private static readonly Color InProgressColor = new(1f, 1f, 0.3f, 0.5f);

    public override void _Ready()
    {
        MaterialOverride = GetOverlayMaterial();
    }

    public override void _Process(double delta)
    {
        RebuildOverlayMesh();
    }

    /// <summary>Rebuild the overlay mesh from current job designations.</summary>
    private void RebuildOverlayMesh()
    {
        if (JobSystem.Instance == null)
        {
            Mesh = null;
            return;
        }

        var jobs = JobSystem.Instance.AllJobs;
        if (jobs.Count == 0)
        {
            Mesh = null;
            return;
        }

        float px = Settings.BlockPixelSize;
        var vertices = new List<Vector3>();
        var colors = new List<Color>();

        foreach (var job in jobs)
        {
            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed)
                continue;

            Color color;
            if (job.Status == JobStatus.InProgress)
            {
                // Pulse effect for in-progress
                float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.005f);
                color = InProgressColor with { A = 0.3f + 0.3f * pulse };
            }
            else if (job.Status == JobStatus.Reserved)
            {
                color = GetJobColor(job.JobType) with { A = 0.25f };
            }
            else
            {
                color = GetJobColor(job.JobType);
            }

            // Quad on XZ plane at Y = 0.01 (slightly above ground to avoid z-fighting)
            float qx = job.TargetBlockCoord.X * px;
            float qz = job.TargetBlockCoord.Y * px;
            float y = 0.01f;

            // Inset slightly for visual clarity
            float inset = px * 0.05f;
            Vector3 tl = new(qx + inset, y, qz + inset);
            Vector3 tr = new(qx + px - inset, y, qz + inset);
            Vector3 br = new(qx + px - inset, y, qz + px - inset);
            Vector3 bl = new(qx + inset, y, qz + px - inset);

            // Two triangles
            vertices.Add(tl); colors.Add(color);
            vertices.Add(bl); colors.Add(color);
            vertices.Add(br); colors.Add(color);

            vertices.Add(tl); colors.Add(color);
            vertices.Add(br); colors.Add(color);
            vertices.Add(tr); colors.Add(color);

            // Progress bar if in progress
            if (job.Status == JobStatus.InProgress && job.Progress > 0f)
            {
                float barY = 0.02f;
                float barWidth = (px - inset * 2) * job.Progress;
                float barHeight = px * 0.08f;
                Color barColor = new(0.2f, 1f, 0.2f, 0.7f);

                Vector3 btl = new(qx + inset, barY, qz + inset);
                Vector3 btr = new(qx + inset + barWidth, barY, qz + inset);
                Vector3 bbr = new(qx + inset + barWidth, barY, qz + inset + barHeight);
                Vector3 bbl = new(qx + inset, barY, qz + inset + barHeight);

                vertices.Add(btl); colors.Add(barColor);
                vertices.Add(bbl); colors.Add(barColor);
                vertices.Add(bbr); colors.Add(barColor);

                vertices.Add(btl); colors.Add(barColor);
                vertices.Add(bbr); colors.Add(barColor);
                vertices.Add(btr); colors.Add(barColor);
            }
        }

        if (vertices.Count == 0)
        {
            Mesh = null;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
    }

    private static Color GetJobColor(string jobType)
    {
        return JobTypeColors.GetValueOrDefault(jobType, new Color(0.8f, 0.8f, 0.8f, 0.4f));
    }

    private static ShaderMaterial GetOverlayMaterial()
    {
        if (_overlayMaterial != null) return _overlayMaterial;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

void fragment() {
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}";
        _overlayMaterial = new ShaderMaterial { Shader = shader };
        return _overlayMaterial;
    }
}
