using Godot;
using Godot.Collections;

[GlobalClass]
public partial class SceneResource : Resource
{
    public const int CurrentFormatVersion = 1;

    [Export]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    [Export]
    public Array<SceneObjectResource> Objects { get; set; } = [];
}
