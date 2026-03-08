shader_type canvas_item;

// Simple shader that uses vertex colors for rendering.
// This is needed because ArrayMesh with vertex colors requires
// a material that reads COLOR from vertices.

void fragment() {
    COLOR = COLOR; // Use vertex color as-is
}
