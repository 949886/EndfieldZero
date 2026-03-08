using System.Collections.Generic;
using EndfieldZero.Core;
using Godot;

namespace EndfieldZero.World;

/// <summary>
/// Generates and renders a mesh for a single chunk using ArrayMesh + MeshInstance3D.
/// Each non-air block becomes a colored quad on the XZ plane (Y=0).
/// Adjacent same-type blocks are merged via greedy meshing to minimize vertices.
/// </summary>
public partial class ChunkRenderer : MeshInstance3D
{
    private Chunk _chunk;
    private static ShaderMaterial _sharedMaterial;

    public void SetChunk(Chunk chunk)
    {
        _chunk = chunk;
        // Apply vertex color material
        MaterialOverride = GetSharedMaterial();
    }

    /// <summary>Shared material that renders vertex colors, unlit (no shading).</summary>
    private static ShaderMaterial GetSharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;

        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

void vertex() {
    // Pass vertex color through
}

void fragment() {
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a;
}";
        _sharedMaterial = new ShaderMaterial { Shader = shader };
        return _sharedMaterial;
    }

    /// <summary>
    /// Rebuild the mesh from chunk data. Call when chunk.IsDirty is true.
    /// Uses greedy meshing to merge adjacent same-type blocks into larger quads.
    /// Quads are placed on the XZ plane at Y=0.
    /// </summary>
    public void RebuildMesh()
    {
        if (_chunk == null) return;

        var registry = BlockRegistry.Instance;
        int size = Constants.ChunkSize;
        float px = Constants.BlockPixelSize;

        // Collect vertices and colors via greedy meshing on layer 0
        var vertices = new List<Vector3>();
        var colors = new List<Color>();

        bool[,] merged = new bool[size, size];

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                if (merged[x, z]) continue;

                Block block = _chunk.GetBlock(x, z, 0);
                if (block.IsAir) continue;

                BlockDef def = registry.GetDef(block.TypeId);
                if (def == null) continue;

                // Greedy expand: find the widest run of same type on this row
                int width = 1;
                while (x + width < size
                    && !merged[x + width, z]
                    && _chunk.GetBlock(x + width, z, 0).TypeId == block.TypeId)
                {
                    width++;
                }

                // Expand downward (in z) as far as the full width matches
                int height = 1;
                bool canExpand = true;
                while (canExpand && z + height < size)
                {
                    for (int dx = 0; dx < width; dx++)
                    {
                        if (merged[x + dx, z + height]
                            || _chunk.GetBlock(x + dx, z + height, 0).TypeId != block.TypeId)
                        {
                            canExpand = false;
                            break;
                        }
                    }
                    if (canExpand) height++;
                }

                // Mark all blocks in this rect as merged
                for (int dz = 0; dz < height; dz++)
                    for (int dx = 0; dx < width; dx++)
                        merged[x + dx, z + dz] = true;

                // Add quad on XZ plane (Y=0) for this merged rectangle
                float qx = x * px;
                float qz = z * px;
                float qw = width * px;
                float qh = height * px;

                // Corners on XZ plane, Y = 0
                Vector3 tl = new(qx, 0, qz);
                Vector3 tr = new(qx + qw, 0, qz);
                Vector3 br = new(qx + qw, 0, qz + qh);
                Vector3 bl = new(qx, 0, qz + qh);

                // Triangle 1: TL → BL → BR (counter-clockwise when viewed from +Y)
                vertices.Add(tl); colors.Add(def.Color);
                vertices.Add(bl); colors.Add(def.Color);
                vertices.Add(br); colors.Add(def.Color);

                // Triangle 2: TL → BR → TR
                vertices.Add(tl); colors.Add(def.Color);
                vertices.Add(br); colors.Add(def.Color);
                vertices.Add(tr); colors.Add(def.Color);
            }
        }

        // Build ArrayMesh
        if (vertices.Count == 0)
        {
            Mesh = null;
            _chunk.IsDirty = false;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        // Convert to packed arrays (Vector3 for 3D)
        var packedVerts = new Vector3[vertices.Count];
        var packedColors = new Color[colors.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            packedVerts[i] = vertices[i];
            packedColors[i] = colors[i];
        }

        arrays[(int)Mesh.ArrayType.Vertex] = packedVerts;
        arrays[(int)Mesh.ArrayType.Color] = packedColors;

        var arrayMesh = new ArrayMesh();
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        Mesh = arrayMesh;
        _chunk.IsDirty = false;
    }
}
