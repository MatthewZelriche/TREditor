using System;
using Godot;

public sealed class EditorToolContext
{
    public EditorToolContext(
        ScenePickingService scenePicking,
        SelectionService selection,
        ObjectSelectionHighlightController objectSelectionHighlight,
        ComponentSelectionHighlightController componentSelectionHighlight,
        SelectionTranslationGizmoController selectionTranslationGizmo,
        Func<float> getGridSnapSize
    )
    {
        ArgumentNullException.ThrowIfNull(scenePicking);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(objectSelectionHighlight);
        ArgumentNullException.ThrowIfNull(componentSelectionHighlight);
        ArgumentNullException.ThrowIfNull(selectionTranslationGizmo);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        ScenePicking = scenePicking;
        Selection = selection;
        ObjectSelectionHighlight = objectSelectionHighlight;
        ComponentSelectionHighlight = componentSelectionHighlight;
        SelectionTranslationGizmo = selectionTranslationGizmo;
        GetGridSnapSize = getGridSnapSize;
    }

    public ScenePickingService ScenePicking { get; }
    public SelectionService Selection { get; }
    public ObjectSelectionHighlightController ObjectSelectionHighlight { get; }
    public ComponentSelectionHighlightController ComponentSelectionHighlight { get; }
    public SelectionTranslationGizmoController SelectionTranslationGizmo { get; }
    public Func<float> GetGridSnapSize { get; }
}
