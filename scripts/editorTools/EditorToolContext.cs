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
        Func<float> getGridSnapSize,
        EditorSceneModel sceneModel,
        Node3D worldRoot,
        IEditorSceneView sceneView
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
        ArgumentNullException.ThrowIfNull(sceneModel);
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(sceneView);

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
        SceneModel = sceneModel;
        WorldRoot = worldRoot;
        SceneView = sceneView;
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
    public EditorSceneModel SceneModel { get; }
    public Node3D WorldRoot { get; }
    public IEditorSceneView SceneView { get; }

    public bool TryGetObject(EditorObjectId objectId, out EditorObjectModel obj) =>
        SceneModel.TryGet(objectId, out obj);

    public Transform3D GetObjectGlobalTransform(EditorObjectModel obj) =>
        WorldRoot.GlobalTransform * obj.LocalTransform;
}
