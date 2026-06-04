using System;
using Godot;

public sealed class EditorToolContext
{
    public EditorToolContext(ScenePickingService scenePicking, Func<float> getGridSnapSize)
    {
        ArgumentNullException.ThrowIfNull(scenePicking);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        ScenePicking = scenePicking;
        GetGridSnapSize = getGridSnapSize;
    }

    public ScenePickingService ScenePicking { get; }
    public Func<float> GetGridSnapSize { get; }
}
