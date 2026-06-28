using Godot;
using Godot.Collections;

namespace Cherry.Research;

[GlobalClass]
public partial class TechnologyDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public int ResearchTicks { get; set; } = 300;
    [Export] public Texture2D Icon { get; set; }
    [Export] public Vector2 CanvasPosition { get; set; }
    [Export] public Array<string> PrerequisiteIds { get; set; } = new();
    [Export] public Array<TechnologyEffectDef> Effects { get; set; } = new();
}
