using System;
using Godot;

public sealed class EditorToolContext
{
    public EditorToolContext(
        ScenePickingService scenePicking,
        SelectionService selection,
        Func<float> getGridSnapSize
    )
    {
        ArgumentNullException.ThrowIfNull(scenePicking);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        ScenePicking = scenePicking;
        Selection = selection;
        GetGridSnapSize = getGridSnapSize;
    }

    public ScenePickingService ScenePicking { get; }
    public SelectionService Selection { get; }
    public Func<float> GetGridSnapSize { get; }
}
