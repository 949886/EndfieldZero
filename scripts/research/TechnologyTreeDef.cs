using Godot;
using Godot.Collections;

namespace Cherry.Research;

[GlobalClass]
public partial class TechnologyTreeDef : Resource
{
    [Export] public Array<TechnologyDef> Technologies { get; set; } = new();
}
