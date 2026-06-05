using System;
using Godot;

public sealed class EditorToolContext
{
    public EditorToolContext(
        ScenePickingService scenePicking,
        SelectionService selection,
        ComponentSelectionHighlightController componentSelectionHighlight,
        Func<float> getGridSnapSize
    )
    {
        ArgumentNullException.ThrowIfNull(scenePicking);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(componentSelectionHighlight);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        ScenePicking = scenePicking;
        Selection = selection;
        ComponentSelectionHighlight = componentSelectionHighlight;
        GetGridSnapSize = getGridSnapSize;
    }

    public ScenePickingService ScenePicking { get; }
    public SelectionService Selection { get; }
    public ComponentSelectionHighlightController ComponentSelectionHighlight { get; }
    public Func<float> GetGridSnapSize { get; }
}
