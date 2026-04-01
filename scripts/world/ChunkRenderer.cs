using System.Collections.Generic;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Generates a lightweight blocky mesh for a single chunk.
/// Both camera modes render the highest visible top face for each column,
/// while angled mode also exposes vertical faces for raised columns.
/// </summary>
public partial class ChunkRenderer : MeshInstance3D
{
    private sealed class CellVisual
    {
        public Block Block;
        public BlockDef Def;
        public BlockVisualKind VisualKind;
        public Color Color;
        public float BaseY;
        public float SurfaceY;
        public float DecorationTopY;
        public bool HasSurface;
    }

    private Chunk _chunk;
    private static ShaderMaterial _angledMaterial;
    private static ShaderMaterial _topDownMaterial;

    public void SetChunk(Chunk chunk)
    {
        _chunk = chunk;
        CastShadow = ShadowCastingSetting.On;
        ApplyMaterialForCurrentView();
    }

    public static void UpdateSharedViewState(
        Vector2 mouseScreenUv,
        Vector2 selectedScreenUv,
        Vector2 occlusionRadiusUv,
        bool occlusionEnabled,
        bool selectedOcclusionEnabled,
        float occlusionAlpha)
    {
        if (_angledMaterial == null)
            return;

        _angledMaterial.SetShaderParameter("occlusion_mouse_screen_uv", mouseScreenUv);
        _angledMaterial.SetShaderParameter("occlusion_selected_screen_uv", selectedScreenUv);
        _angledMaterial.SetShaderParameter("occlusion_radius_uv", occlusionRadiusUv);
        _angledMaterial.SetShaderParameter("occlusion_enabled", occlusionEnabled);
        _angledMaterial.SetShaderParameter("selected_occlusion_enabled", selectedOcclusionEnabled);
        _angledMaterial.SetShaderParameter("occlusion_alpha", occlusionAlpha);
    }

    private static ShaderMaterial GetAngledMaterial()
    {
        if (_angledMaterial != null)
            return _angledMaterial;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_prepass_alpha;

uniform bool occlusion_enabled = false;
uniform bool selected_occlusion_enabled = false;
uniform vec2 occlusion_mouse_screen_uv = vec2(0.5);
uniform vec2 occlusion_selected_screen_uv = vec2(-10.0);
uniform vec2 occlusion_radius_uv = vec2(0.12);
uniform float occlusion_alpha = 0.22;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
    float alpha = COLOR.a;

    if (occlusion_enabled && abs(world_normal.y) < 0.35) {
        vec2 radius = max(occlusion_radius_uv, vec2(0.0001));
        float mouse_distance = length((SCREEN_UV - occlusion_mouse_screen_uv) / radius);
        float mouse_fade = 1.0 - smoothstep(1.0, 1.75, mouse_distance);

        float selected_fade = 0.0;
        if (selected_occlusion_enabled) {
            float selected_distance = length((SCREEN_UV - occlusion_selected_screen_uv) / radius);
            selected_fade = 1.0 - smoothstep(1.0, 1.75, selected_distance);
        }

        float fade = max(mouse_fade, selected_fade);

        alpha = mix(alpha, min(alpha, occlusion_alpha), fade);
    }

    ALBEDO = COLOR.rgb;

    if (alpha < 0.999) {
        ALPHA = alpha;
    }
}";
        _angledMaterial = new ShaderMaterial { Shader = shader };
        return _angledMaterial;
    }

    private static ShaderMaterial GetTopDownMaterial()
    {
        if (_topDownMaterial != null)
            return _topDownMaterial;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

void fragment() {
    ALBEDO = COLOR.rgb;
}";
        _topDownMaterial = new ShaderMaterial { Shader = shader };
        return _topDownMaterial;
    }

    public void RebuildMesh()
    {
        if (_chunk == null)
            return;

        ApplyMaterialForCurrentView();

        int size = Settings.ChunkSize;
        float px = Settings.BlockPixelSize;
        var visuals = BuildCellVisuals(size, px);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();

        bool angledView = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;
        BuildTopFaces(visuals, size, px, vertices, normals, colors);

        if (angledView)
        {
            BuildSideFaces(visuals, size, px, vertices, normals, colors);
            BuildCrossPlanes(visuals, size, px, vertices, normals, colors);
        }

        if (vertices.Count == 0)
        {
            Mesh = null;
            _chunk.IsDirty = false;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
        _chunk.IsDirty = false;
    }

    private void ApplyMaterialForCurrentView()
    {
        bool angledView = GameCamera.Instance?.ViewMode == CameraViewMode.Angled3D;
        MaterialOverride = angledView ? GetAngledMaterial() : GetTopDownMaterial();
        CastShadow = angledView ? ShadowCastingSetting.On : ShadowCastingSetting.Off;
    }

    private CellVisual[,] BuildCellVisuals(int size, float px)
    {
        var visuals = new CellVisual[size, size];
        var registry = BlockRegistry.Instance;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                Block block = Block.Air;
                BlockDef def = null;
                int layer = 0;

                for (int testLayer = Settings.MaxLayers - 1; testLayer >= 0; testLayer--)
                {
                    block = _chunk.GetBlock(x, z, testLayer);
                    if (block.IsAir)
                        continue;

                    def = registry.GetDef(block.TypeId);
                    if (def != null)
                    {
                        layer = testLayer;
                        break;
                    }
                }

                if (def == null)
                {
                    visuals[x, z] = new CellVisual();
                    continue;
                }

                float baseY = layer * px;
                visuals[x, z] = new CellVisual
                {
                    Block = block,
                    Def = def,
                    VisualKind = def.VisualKind,
                    Color = def.Color,
                    BaseY = baseY,
                    SurfaceY = baseY + def.GetSurfaceHeight(px),
                    DecorationTopY = baseY + def.GetDecorationHeight(px),
                    HasSurface = true,
                };
            }
        }

        return visuals;
    }

    private void BuildTopFaces(CellVisual[,] visuals, int size, float px, List<Vector3> vertices, List<Vector3> normals, List<Color> colors)
    {
        var merged = new bool[size, size];

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                if (merged[x, z])
                    continue;

                CellVisual cell = visuals[x, z];
                if (cell == null || !cell.HasSurface)
                    continue;

                int width = 1;
                while (x + width < size && CanMergeTop(cell, visuals[x + width, z], merged[x + width, z]))
                    width++;

                int height = 1;
                bool canExpand = true;
                while (canExpand && z + height < size)
                {
                    for (int dx = 0; dx < width; dx++)
                    {
                        if (!CanMergeTop(cell, visuals[x + dx, z + height], merged[x + dx, z + height]))
                        {
                            canExpand = false;
                            break;
                        }
                    }

                    if (canExpand)
                        height++;
                }

                for (int dz = 0; dz < height; dz++)
                {
                    for (int dx = 0; dx < width; dx++)
                        merged[x + dx, z + dz] = true;
                }

                float left = x * px;
                float top = z * px;
                float right = (x + width) * px;
                float bottom = (z + height) * px;
                float y = cell.SurfaceY;

                Vector3 tl = new(left, y, top);
                Vector3 tr = new(right, y, top);
                Vector3 br = new(right, y, bottom);
                Vector3 bl = new(left, y, bottom);
                AddQuad(vertices, normals, colors, tl, tr, br, bl, Vector3.Up, cell.Color);
            }
        }
    }

    private void BuildSideFaces(CellVisual[,] visuals, int size, float px, List<Vector3> vertices, List<Vector3> normals, List<Color> colors)
    {
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                CellVisual cell = visuals[x, z];
                if (cell == null || !cell.HasSurface)
                    continue;

                float westNeighbor = GetNeighborSurfaceY(visuals, x - 1, z, x, z, new Vector2I(-1, 0));
                float eastNeighbor = GetNeighborSurfaceY(visuals, x + 1, z, x, z, new Vector2I(1, 0));
                float northNeighbor = GetNeighborSurfaceY(visuals, x, z - 1, x, z, new Vector2I(0, -1));
                float southNeighbor = GetNeighborSurfaceY(visuals, x, z + 1, x, z, new Vector2I(0, 1));

                float left = x * px;
                float right = (x + 1) * px;
                float top = z * px;
                float bottom = (z + 1) * px;

                if (cell.SurfaceY > westNeighbor + 0.001f)
                {
                    Vector3 tl = new(left, cell.SurfaceY, top);
                    Vector3 tr = new(left, cell.SurfaceY, bottom);
                    Vector3 br = new(left, westNeighbor, bottom);
                    Vector3 bl = new(left, westNeighbor, top);
                    AddQuad(vertices, normals, colors, tl, tr, br, bl, Vector3.Left, TintForSide(cell.Color, Vector3.Left));
                }

                if (cell.SurfaceY > eastNeighbor + 0.001f)
                {
                    Vector3 tl = new(right, cell.SurfaceY, bottom);
                    Vector3 tr = new(right, cell.SurfaceY, top);
                    Vector3 br = new(right, eastNeighbor, top);
                    Vector3 bl = new(right, eastNeighbor, bottom);
                    AddQuad(vertices, normals, colors, tl, tr, br, bl, Vector3.Right, TintForSide(cell.Color, Vector3.Right));
                }

                if (cell.SurfaceY > northNeighbor + 0.001f)
                {
                    Vector3 tl = new(right, cell.SurfaceY, top);
                    Vector3 tr = new(left, cell.SurfaceY, top);
                    Vector3 br = new(left, northNeighbor, top);
                    Vector3 bl = new(right, northNeighbor, top);
                    AddQuad(vertices, normals, colors, tl, tr, br, bl, Vector3.Forward, TintForSide(cell.Color, Vector3.Forward));
                }

                if (cell.SurfaceY > southNeighbor + 0.001f)
                {
                    Vector3 tl = new(left, cell.SurfaceY, bottom);
                    Vector3 tr = new(right, cell.SurfaceY, bottom);
                    Vector3 br = new(right, southNeighbor, bottom);
                    Vector3 bl = new(left, southNeighbor, bottom);
                    AddQuad(vertices, normals, colors, tl, tr, br, bl, Vector3.Back, TintForSide(cell.Color, Vector3.Back));
                }
            }
        }
    }

    private void BuildCrossPlanes(CellVisual[,] visuals, int size, float px, List<Vector3> vertices, List<Vector3> normals, List<Color> colors)
    {
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                CellVisual cell = visuals[x, z];
                if (cell == null || !cell.HasSurface || cell.VisualKind != BlockVisualKind.Cross)
                    continue;

                float half = px * 0.38f;
                float centerX = x * px + px * 0.5f;
                float centerZ = z * px + px * 0.5f;
                float baseY = cell.SurfaceY;
                float topY = cell.DecorationTopY;

                Vector3 a1 = new(centerX - half, topY, centerZ - half);
                Vector3 a2 = new(centerX + half, topY, centerZ + half);
                Vector3 a3 = new(centerX + half, baseY, centerZ + half);
                Vector3 a4 = new(centerX - half, baseY, centerZ - half);
                AddQuad(vertices, normals, colors, a1, a2, a3, a4, new Vector3(1f, 0f, 1f).Normalized(), cell.Color);

                Vector3 b1 = new(centerX + half, topY, centerZ - half);
                Vector3 b2 = new(centerX - half, topY, centerZ + half);
                Vector3 b3 = new(centerX - half, baseY, centerZ + half);
                Vector3 b4 = new(centerX + half, baseY, centerZ - half);
                AddQuad(vertices, normals, colors, b1, b2, b3, b4, new Vector3(-1f, 0f, 1f).Normalized(), cell.Color);
            }
        }
    }

    private float GetNeighborSurfaceY(CellVisual[,] visuals, int localX, int localZ, int cellX, int cellZ, Vector2I offset)
    {
        if (localX >= 0 && localX < Settings.ChunkSize && localZ >= 0 && localZ < Settings.ChunkSize)
        {
            CellVisual neighbor = visuals[localX, localZ];
            return neighbor != null && neighbor.HasSurface ? neighbor.SurfaceY : 0f;
        }

        if (WorldManager.Instance == null)
            return 0f;

        int worldX = _chunk.WorldOrigin.X + cellX + offset.X;
        int worldZ = _chunk.WorldOrigin.Y + cellZ + offset.Y;
        return WorldManager.Instance.GetSurfaceTopY(worldX, worldZ);
    }

    private static bool CanMergeTop(CellVisual seed, CellVisual candidate, bool alreadyMerged)
    {
        return !alreadyMerged
            && candidate != null
            && candidate.HasSurface
            && candidate.Block.TypeId == seed.Block.TypeId
            && candidate.VisualKind == seed.VisualKind
            && Mathf.IsEqualApprox(candidate.SurfaceY, seed.SurfaceY);
    }

    private static Color TintForSide(Color color, Vector3 normal)
    {
        float brightness = normal switch
        {
            var n when n == Vector3.Left => 0.82f,
            var n when n == Vector3.Right => 0.88f,
            var n when n == Vector3.Forward => 0.78f,
            var n when n == Vector3.Back => 0.72f,
            _ => 0.8f,
        };

        Color tinted = color * brightness;
        tinted.A = color.A;
        return tinted;
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Color> colors,
        Vector3 tl,
        Vector3 tr,
        Vector3 br,
        Vector3 bl,
        Vector3 normal,
        Color color)
    {
        AddVertex(vertices, normals, colors, tl, normal, color);
        AddVertex(vertices, normals, colors, bl, normal, color);
        AddVertex(vertices, normals, colors, br, normal, color);

        AddVertex(vertices, normals, colors, tl, normal, color);
        AddVertex(vertices, normals, colors, br, normal, color);
        AddVertex(vertices, normals, colors, tr, normal, color);
    }

    private static void AddVertex(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, Vector3 vertex, Vector3 normal, Color color)
    {
        vertices.Add(vertex);
        normals.Add(normal);
        colors.Add(color);
    }
}
