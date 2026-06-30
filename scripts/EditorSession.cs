using System;
using Gizmo3DPlugin;
using Godot;

public partial class EditorSession : Node3D
{
    public CommandService Commands { get; private set; }

    public SelectionService Selection { get; private set; }

    public ScenePickingService ScenePicking { get; private set; }

    public TextureMaterialLibrary TextureMaterials { get; private set; }

    public TextureAssetCatalog TextureCatalog { get; private set; }

    public TextureRootSettingsService TextureRootSettings { get; private set; }

    public DocumentService Document { get; private set; }

    public EditOperationSettings EditOperationSettings { get; private set; }

    public PrimitiveCreationSettings PrimitiveCreationSettings { get; set; } =
        PrimitiveCreationSettings.Box();

    public float GridSnapSize
    {
        get => _gridSnapSize;
        set
        {
            float snapSize = Mathf.Max(GridSnap.Off, value);
            if (_gridSnapSize == snapSize)
                return;

            _gridSnapSize = snapSize;
            GridSnapSizeChanged?.Invoke();
            if (
                EditOperationSettings?.IsSelected("InsetFace") == true
                || EditOperationSettings?.IsSelected("BevelEdge") == true
                || EditOperationSettings?.IsSelected("BevelVertex") == true
            )
                ApplyEditOperationSettings();
        }
    }

    public EditorToolId ActivePersistentTool =>
        _toolManager?.PersistentToolId ?? EditorToolId.Select;

    public string StatusMessage { get; private set; } = "";

    public event Action<EditorToolId> PersistentToolChanged;
    public event Action GridSnapSizeChanged;
    public event Action<string> StatusMessageChanged;

    private float _gridSnapSize = GridSnap.Off;
    private EditorToolContext _toolContext;
    private EditorToolManager _toolManager;
    private EditorPreviewService _previewService;

    // TODO: REALLY don't like how we're jamming so many controllers into this class.
    private ObjectSelectionHighlightController _objectSelectionHighlightController;
    private SelectionTranslationGizmoController _selectionTranslationGizmoController;
    private ComponentSelectionHighlightController _componentSelectionHighlightController;
    private EditorSceneService _sceneService;
    private bool _translationGizmoEventsWired;

    // TODO: Feels like this shouldn't be such a globally available state.
    private bool _canCollapseFace;
    private bool _canCollapseVertices;
    private bool _canBridgeEdges;
    private bool _canDetachFaces;
    private bool _canFillHole;
    private float _maximumInsetDepth;
    private float _maximumBevelWidth;
    private string _maximumBevelOperationId;
    private CollapseVerticesTarget? _validatedCollapseVerticesTarget;

    public override void _EnterTree()
    {
        ScenePicking = new ScenePickingService(GetWorld3D());
        Selection = new SelectionService();
        EditOperationSettings = new EditOperationSettings();
        Selection.SelectionChanged += OnSelectionChangedForEditOperation;
        TextureRootSettings = new TextureRootSettingsService();
        TextureCatalog = new TextureAssetCatalog();
        TextureCatalog.Rescan(TextureRootSettings.RootPath);
        TextureMaterials = new TextureMaterialLibrary(assetId =>
            TextureRootSettings.RootPath is string textureRoot
                ? TextureFileLoader.Load(textureRoot, assetId)
                : null
        );
        _sceneService = new EditorSceneService(this, TextureMaterials);
        _objectSelectionHighlightController = new ObjectSelectionHighlightController(
            _sceneService,
            Selection
        );
        _componentSelectionHighlightController = new ComponentSelectionHighlightController(
            _sceneService,
            Selection
        );
        _selectionTranslationGizmoController = new SelectionTranslationGizmoController(
            _sceneService,
            Selection
        );
        EditOperationSettings.Changed += ApplyEditOperationSettings;
        ApplyEditOperationSettings();
        Commands = new CommandService(new EditorCommandContext(_sceneService, Selection));
        Commands.CommandHistoryChanged += OnCommandHistoryChanged;
        Document = new DocumentService(_sceneService, TextureMaterials, Selection, Commands);
    }

    public override void _Ready()
    {
        EnsureToolManager();
    }

    public override void _Process(double delta)
    {
        TextureCatalog?.ProcessPreviewQueue();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (IsModalEditOperationSelected())
        {
            bool operationHandled = key.Keycode switch
            {
                Key.Enter or Key.KpEnter => ApplySelectedEditOperation(),
                Key.Escape => CancelSelectedEditOperation(),
                _ => false,
            };
            if (operationHandled)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        bool handled =
            key.Keycode == Key.Escape
                ? _toolManager?.CancelTemporaryTool() == true
                    || _toolManager?.HandleKey(key.Keycode) == true
                : _toolManager?.HandleKey(key.Keycode) == true;

        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public void ActivatePersistentTool(EditorToolId toolId)
    {
        EnsureToolManager();
        _toolManager.ActivatePersistentTool(toolId);
    }

    public void ReportStatus(string message)
    {
        message ??= "";
        if (StatusMessage == message)
            return;

        StatusMessage = message;
        StatusMessageChanged?.Invoke(message);
    }

    public bool TrySetTextureRoot(string rootPath)
    {
        if (!TextureRootSettings.TrySetRootPath(rootPath))
            return false;

        RefreshTextureCatalog();
        return true;
    }

    public void ClearTextureRoot()
    {
        TextureRootSettings.ClearRootPath();
        RefreshTextureCatalog();
    }

    public void RefreshTextureCatalog()
    {
        TextureCatalog.Rescan(TextureRootSettings.RootPath);
        TextureMaterials.ClearResolvedMaterials();
    }

    public void RegisterSelectionTranslationGizmo(Gizmo3D gizmo, Node3D target)
    {
        _selectionTranslationGizmoController?.Register(gizmo, target);
    }

    public void UnregisterSelectionTranslationGizmo(Gizmo3D gizmo)
    {
        _selectionTranslationGizmoController?.Unregister(gizmo);
    }

    public bool ApplySelectedEditOperation()
    {
        EditorCommand command = EditOperationSettings.SelectedOperationId switch
        {
            "InsetFace" => InsetFaceCommand.Create(Selection.Current, GetEffectiveInsetDepth()),
            "BevelEdge" => BevelEdgeCommand.Create(Selection.Current, GetEffectiveBevelWidth()),
            "BevelVertex" => BevelVertexCommand.Create(Selection.Current, GetEffectiveBevelWidth()),
            "CollapseVertices" => CollapseVerticesCommand.Create(
                Selection.Current,
                EditOperationSettings.TwoVertexCollapseTarget
            ),
            "BridgeEdges" => BridgeEdgesCommand.Create(
                Selection.Current,
                EditOperationSettings.BridgeSegments,
                EditOperationSettings.BridgeArchAngleDegrees
            ),
            "DetachFace" => DetachFaceCommand.Create(Selection.Current),
            "FillHole" when _canFillHole => FillHoleCommand.Create(Selection.Current),
            "CollapseFace" when _canCollapseFace => CollapseFaceCommand.Create(Selection.Current),
            _ => null,
        };
        if (command == null)
            return false;

        _previewService?.Apply(new EditorPreviewRequest.Clear());
        EditOperationSettings.Deselect();
        Commands.Execute(command);
        return true;
    }

    public bool TryGetMaximumSelectedFaceInsetDepth(out float maximumDepth)
    {
        maximumDepth = _maximumInsetDepth;
        return EditOperationSettings.IsSelected("InsetFace") && maximumDepth > 0f;
    }

    public float GetSnappedInsetDepth() =>
        TryGetMaximumSelectedFaceInsetDepth(out float maximumDepth)
            ? GridSnap.SnapDistance(EditOperationSettings.InsetDepth, GridSnapSize, maximumDepth)
            : EditOperationSettings.InsetDepth;

    public bool TryGetMaximumSelectedBevelWidth(out float maximumWidth)
    {
        maximumWidth = _maximumBevelWidth;
        return (
                EditOperationSettings.IsSelected("BevelEdge")
                || EditOperationSettings.IsSelected("BevelVertex")
            )
            && maximumWidth > 0f;
    }

    public float GetSnappedBevelWidth() =>
        TryGetMaximumSelectedBevelWidth(out float maximumWidth)
            ? GridSnap.SnapDistance(EditOperationSettings.BevelWidth, GridSnapSize, maximumWidth)
            : EditOperationSettings.BevelWidth;

    public bool CanApplySelectedEditOperation() =>
        EditOperationSettings.SelectedOperationId switch
        {
            "InsetFace" => _maximumInsetDepth > 0f,
            "BevelEdge" => _maximumBevelWidth > 0f,
            "BevelVertex" => _maximumBevelWidth > 0f,
            "CollapseVertices" => _canCollapseVertices,
            "BridgeEdges" => _canBridgeEdges,
            "DetachFace" => _canDetachFaces,
            "FillHole" => _canFillHole,
            "CollapseFace" => _canCollapseFace,
            _ => false,
        };

    public bool CancelSelectedEditOperation()
    {
        if (!IsModalEditOperationSelected())
            return false;

        _previewService?.Apply(new EditorPreviewRequest.Clear());
        EditOperationSettings.Deselect();
        return true;
    }

    public override void _ExitTree()
    {
        if (_toolManager != null)
        {
            _toolManager.CommandSubmitted -= Commands.Execute;
            _toolManager.PreviewSubmitted -= _previewService.Apply;
            _toolManager.PersistentToolChanged -= OnPersistentToolChanged;
        }

        if (_translationGizmoEventsWired)
        {
            _selectionTranslationGizmoController.CommandSubmitted -= Commands.Execute;
            _selectionTranslationGizmoController.PreviewSubmitted -= _previewService.Apply;
            _translationGizmoEventsWired = false;
        }

        _toolManager?.Dispose();
        _toolManager = null;
        _previewService?.Dispose();
        _previewService = null;
        _selectionTranslationGizmoController?.Dispose();
        _selectionTranslationGizmoController = null;
        EditOperationSettings.Changed -= ApplyEditOperationSettings;
        Selection.SelectionChanged -= OnSelectionChangedForEditOperation;
        _objectSelectionHighlightController?.Dispose();
        _objectSelectionHighlightController = null;
        _componentSelectionHighlightController?.Dispose();
        _componentSelectionHighlightController = null;
        // Commands may own topology patches that must release reserved handles before the
        // scene service disposes their meshes.
        Commands.CommandHistoryChanged -= OnCommandHistoryChanged;
        Commands.Dispose();
        _sceneService?.Dispose();
        _sceneService = null;
        Selection?.Dispose();
        Selection = null;
    }

    private void EnsureToolManager()
    {
        if (_toolManager != null)
        {
            return;
        }

        _toolContext = new EditorToolContext(
            ScenePicking,
            Selection,
            _objectSelectionHighlightController,
            _componentSelectionHighlightController,
            _selectionTranslationGizmoController,
            EditOperationSettings,
            TextureCatalog,
            TextureMaterials,
            ReportStatus,
            () => GridSnapSize,
            objectId =>
                _sceneService.TryGetMeshNode(objectId, out TRMeshGD meshNode) ? meshNode : null
        );
        _previewService = new EditorPreviewService(
            this,
            _sceneService,
            _componentSelectionHighlightController.Refresh
        );
        _toolManager = new EditorToolManager(_toolContext, () => PrimitiveCreationSettings);
        _toolManager.CommandSubmitted += Commands.Execute;
        _toolManager.PreviewSubmitted += _previewService.Apply;
        _toolManager.PersistentToolChanged += OnPersistentToolChanged;
        _selectionTranslationGizmoController.CommandSubmitted += Commands.Execute;
        _selectionTranslationGizmoController.PreviewSubmitted += _previewService.Apply;
        _translationGizmoEventsWired = true;
        ApplyEditOperationSettings();
    }

    private void OnPersistentToolChanged(EditorToolId toolId)
    {
        if (toolId != EditorToolId.Edit && IsModalEditOperationSelected())
            CancelSelectedEditOperation();

        ReportStatus("");
        PersistentToolChanged?.Invoke(toolId);
    }

    private void OnCommandHistoryChanged()
    {
        _componentSelectionHighlightController.Refresh();
        _selectionTranslationGizmoController.Refresh();
    }

    private void ApplyEditOperationSettings()
    {
        bool insetSelected = EditOperationSettings.IsSelected("InsetFace");
        bool bevelEdgeSelected = EditOperationSettings.IsSelected("BevelEdge");
        bool bevelVertexSelected = EditOperationSettings.IsSelected("BevelVertex");
        bool edgeCutSelected = EditOperationSettings.IsSelected("EdgeCut");
        bool collapseVerticesSelected = EditOperationSettings.IsSelected("CollapseVertices");
        bool bridgeEdgesSelected = EditOperationSettings.IsSelected("BridgeEdges");
        bool detachFacesSelected = EditOperationSettings.IsSelected("DetachFace");
        bool fillHoleSelected = EditOperationSettings.IsSelected("FillHole");
        bool collapseFaceSelected = EditOperationSettings.IsSelected("CollapseFace");
        bool modalOperationSelected =
            insetSelected
            || bevelEdgeSelected
            || bevelVertexSelected
            || collapseVerticesSelected
            || bridgeEdgesSelected
            || detachFacesSelected
            || fillHoleSelected
            || collapseFaceSelected;
        _selectionTranslationGizmoController.SetExtrudeOperation(
            EditOperationSettings.IsSelected("ExtrudeFace"),
            EditOperationSettings.ExtrudeAlongFaceNormal
        );
        _selectionTranslationGizmoController.SetInputSuppressed(
            modalOperationSelected || edgeCutSelected
        );

        string bevelOperationId =
            bevelEdgeSelected ? "BevelEdge"
            : bevelVertexSelected ? "BevelVertex"
            : null;
        if (_maximumBevelOperationId != bevelOperationId)
        {
            _maximumBevelOperationId = bevelOperationId;
            _maximumBevelWidth = 0f;
        }

        if (!insetSelected)
            _maximumInsetDepth = 0f;
        else if (
            !(_maximumInsetDepth > 0f)
            && InsetFaceCommand.CanCreate(Selection.Current)
            && _sceneService.TryGetMaximumFaceInsetDepth(
                Selection.Current.Targets[0],
                out float maximumInsetDepth
            )
        )
            _maximumInsetDepth = maximumInsetDepth;

        if (bevelOperationId == null)
            _maximumBevelWidth = 0f;
        else if (!(_maximumBevelWidth > 0f))
        {
            if (
                bevelEdgeSelected
                && BevelEdgeCommand.CanCreate(Selection.Current)
                && _sceneService.TryGetMaximumEdgeBevelWidth(
                    Selection.Current.Targets,
                    out float maximumEdgeBevelWidth
                )
            )
            {
                _maximumBevelWidth = maximumEdgeBevelWidth;
            }
            else if (
                bevelVertexSelected
                && BevelVertexCommand.CanCreate(Selection.Current)
                && _sceneService.TryGetMaximumVertexBevelWidth(
                    Selection.Current.Targets,
                    out float maximumVertexBevelWidth
                )
            )
            {
                _maximumBevelWidth = maximumVertexBevelWidth;
            }
        }

        if (!fillHoleSelected)
            _canFillHole = false;
        else if (
            !_canFillHole
            && FillHoleCommand.CanCreate(Selection.Current)
            && _sceneService.CanFillHole(Selection.Current.Targets[0])
        )
            _canFillHole = true;

        if (!collapseFaceSelected)
            _canCollapseFace = false;
        else if (
            !_canCollapseFace
            && CollapseFaceCommand.CanCreate(Selection.Current)
            && _sceneService.CanCollapseFace(Selection.Current.Targets[0])
        )
            _canCollapseFace = true;

        CollapseVerticesTarget collapseVerticesTarget =
            EditOperationSettings.TwoVertexCollapseTarget;
        if (!collapseVerticesSelected)
        {
            _canCollapseVertices = false;
            _validatedCollapseVerticesTarget = null;
        }
        else
        {
            if (_validatedCollapseVerticesTarget != collapseVerticesTarget)
            {
                _previewService?.Apply(new EditorPreviewRequest.Clear());
                _canCollapseVertices = false;
                _validatedCollapseVerticesTarget = collapseVerticesTarget;
            }

            if (
                !_canCollapseVertices
                && CollapseVerticesCommand.CanCreate(Selection.Current)
                && _sceneService.CanCollapseVertices(
                    Selection.Current.Targets,
                    collapseVerticesTarget
                )
            )
                _canCollapseVertices = true;
        }

        if (!bridgeEdgesSelected)
            _canBridgeEdges = false;
        else if (
            !_canBridgeEdges
            && BridgeEdgesCommand.CanCreate(Selection.Current)
            && _sceneService.CanBridgeEdges(
                Selection.Current.Targets[0],
                Selection.Current.Targets[1]
            )
        )
            _canBridgeEdges = true;

        if (!detachFacesSelected)
            _canDetachFaces = false;
        else if (
            !_canDetachFaces
            && DetachFaceCommand.CanCreate(Selection.Current)
            && _sceneService.CanDetachFaces(Selection.Current.Targets)
        )
            _canDetachFaces = true;

        if (_previewService == null)
            return;

        if (
            insetSelected
            && InsetFaceCommand.CanCreate(Selection.Current)
            && _maximumInsetDepth > 0f
        )
        {
            _previewService.Apply(
                new EditorPreviewRequest.InsetFace(
                    Selection.Current.Targets[0],
                    GetEffectiveInsetDepth()
                )
            );
        }
        else if (
            bevelEdgeSelected
            && BevelEdgeCommand.CanCreate(Selection.Current)
            && _maximumBevelWidth > 0f
        )
        {
            _previewService.Apply(
                new EditorPreviewRequest.BevelEdges(Selection.Current, GetEffectiveBevelWidth())
            );
        }
        else if (
            bevelVertexSelected
            && BevelVertexCommand.CanCreate(Selection.Current)
            && _maximumBevelWidth > 0f
        )
        {
            _previewService.Apply(
                new EditorPreviewRequest.BevelVertices(Selection.Current, GetEffectiveBevelWidth())
            );
        }
        else if (fillHoleSelected && FillHoleCommand.CanCreate(Selection.Current) && _canFillHole)
        {
            _previewService.Apply(new EditorPreviewRequest.FillHole(Selection.Current.Targets[0]));
        }
        else if (
            collapseFaceSelected
            && CollapseFaceCommand.CanCreate(Selection.Current)
            && _canCollapseFace
        )
        {
            _previewService.Apply(
                new EditorPreviewRequest.CollapseFace(Selection.Current.Targets[0])
            );
        }
        else if (
            collapseVerticesSelected
            && CollapseVerticesCommand.CanCreate(Selection.Current)
            && _canCollapseVertices
        )
        {
            _previewService.Apply(
                new EditorPreviewRequest.CollapseVertices(
                    Selection.Current,
                    EditOperationSettings.TwoVertexCollapseTarget
                )
            );
        }
        else if (
            bridgeEdgesSelected
            && BridgeEdgesCommand.CanCreate(Selection.Current)
            && _canBridgeEdges
        )
        {
            _previewService.Apply(
                new EditorPreviewRequest.BridgeEdges(
                    Selection.Current.Targets[0],
                    Selection.Current.Targets[1],
                    EditOperationSettings.BridgeSegments,
                    EditOperationSettings.BridgeArchAngleDegrees
                )
            );
        }
        else if (
            detachFacesSelected
            && DetachFaceCommand.CanCreate(Selection.Current)
            && _canDetachFaces
        )
        {
            _previewService.Apply(new EditorPreviewRequest.DetachFaces(Selection.Current));
        }
        else
        {
            _previewService.Apply(new EditorPreviewRequest.Clear());
        }
    }

    private void OnSelectionChangedForEditOperation()
    {
        if (IsModalEditOperationSelected())
        {
            _previewService?.Apply(new EditorPreviewRequest.Clear());
            _maximumInsetDepth = 0f;
            _maximumBevelWidth = 0f;
            _canFillHole = false;
            _canCollapseFace = false;
            _canCollapseVertices = false;
            _canBridgeEdges = false;
            _canDetachFaces = false;
            ApplyEditOperationSettings();
        }
    }

    private float GetEffectiveInsetDepth() => GetSnappedInsetDepth();

    private float GetEffectiveBevelWidth() => GetSnappedBevelWidth();

    private bool IsModalEditOperationSelected() =>
        EditOperationSettings.IsSelected("InsetFace")
        || EditOperationSettings.IsSelected("BevelEdge")
        || EditOperationSettings.IsSelected("BevelVertex")
        || EditOperationSettings.IsSelected("CollapseVertices")
        || EditOperationSettings.IsSelected("BridgeEdges")
        || EditOperationSettings.IsSelected("DetachFace")
        || EditOperationSettings.IsSelected("FillHole")
        || EditOperationSettings.IsSelected("CollapseFace");
}
