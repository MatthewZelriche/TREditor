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
        EditOperationSettings editOperationSettings,
        TextureAssetCatalog textureCatalog,
        TextureMaterialLibrary textureMaterials,
        Action<string> reportStatus,
        Func<float> getGridSnapSize
    )
    {
        ArgumentNullException.ThrowIfNull(scenePicking);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(objectSelectionHighlight);
        ArgumentNullException.ThrowIfNull(componentSelectionHighlight);
        ArgumentNullException.ThrowIfNull(selectionTranslationGizmo);
        ArgumentNullException.ThrowIfNull(editOperationSettings);
        ArgumentNullException.ThrowIfNull(textureCatalog);
        ArgumentNullException.ThrowIfNull(textureMaterials);
        ArgumentNullException.ThrowIfNull(reportStatus);
        ArgumentNullException.ThrowIfNull(getGridSnapSize);

        ScenePicking = scenePicking;
        Selection = selection;
        ObjectSelectionHighlight = objectSelectionHighlight;
        ComponentSelectionHighlight = componentSelectionHighlight;
        SelectionTranslationGizmo = selectionTranslationGizmo;
        EditOperationSettings = editOperationSettings;
        TextureCatalog = textureCatalog;
        TextureMaterials = textureMaterials;
        ReportStatus = reportStatus;
        GetGridSnapSize = getGridSnapSize;
    }

    public ScenePickingService ScenePicking { get; }
    public SelectionService Selection { get; }
    public ObjectSelectionHighlightController ObjectSelectionHighlight { get; }
    public ComponentSelectionHighlightController ComponentSelectionHighlight { get; }
    public SelectionTranslationGizmoController SelectionTranslationGizmo { get; }
    public EditOperationSettings EditOperationSettings { get; }
    public TextureAssetCatalog TextureCatalog { get; }
    public TextureMaterialLibrary TextureMaterials { get; }
    public Action<string> ReportStatus { get; }
    public Func<float> GetGridSnapSize { get; }
}
