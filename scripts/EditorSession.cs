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
            if (EditOperationSettings?.IsSelected("InsetFace") == true)
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
    private float _maximumInsetDepth;

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

        if (EditOperationSettings.IsSelected("InsetFace"))
        {
            bool insetHandled = key.Keycode switch
            {
                Key.Enter or Key.KpEnter => ApplySelectedEditOperation(),
                Key.Escape => CancelSelectedEditOperation(),
                _ => false,
            };
            if (insetHandled)
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
        if (!EditOperationSettings.IsSelected("InsetFace"))
            return false;

        InsetFaceCommand command = InsetFaceCommand.Create(
            Selection.Current,
            GetEffectiveInsetDepth()
        );
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

    public bool CancelSelectedEditOperation()
    {
        if (!EditOperationSettings.IsSelected("InsetFace"))
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
            () => GridSnapSize
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
        if (toolId != EditorToolId.Edit && EditOperationSettings.IsSelected("InsetFace"))
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
        _selectionTranslationGizmoController.SetExtrudeOperation(
            EditOperationSettings.IsSelected("ExtrudeFace"),
            EditOperationSettings.ExtrudeAlongFaceNormal
        );
        _selectionTranslationGizmoController.SetInputSuppressed(insetSelected);

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
        else
        {
            _previewService.Apply(new EditorPreviewRequest.Clear());
        }
    }

    private void OnSelectionChangedForEditOperation()
    {
        if (EditOperationSettings.IsSelected("InsetFace"))
        {
            _previewService?.Apply(new EditorPreviewRequest.Clear());
            _maximumInsetDepth = 0f;
            ApplyEditOperationSettings();
        }
    }

    private float GetEffectiveInsetDepth() =>
        GetSnappedInsetDepth();
}
