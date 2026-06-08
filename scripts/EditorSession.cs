using Gizmo3DPlugin;
using Godot;

public partial class EditorSession : Node3D
{
    public CommandService Commands { get; private set; }

    public SelectionService Selection { get; private set; }

    public ScenePickingService ScenePicking { get; private set; }

    public TextureMaterialLibrary TextureMaterials { get; private set; }

    public PrimitiveCreationSettings PrimitiveCreationSettings { get; set; } =
        PrimitiveCreationSettings.Box();

    public float GridSnapSize
    {
        get => _gridSnapSize;
        set => _gridSnapSize = Mathf.Max(GridSnap.Off, value);
    }

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

    public override void _EnterTree()
    {
        ScenePicking = new ScenePickingService(GetWorld3D());
        Selection = new SelectionService();
        TextureMaterials = new TextureMaterialLibrary();
        _sceneService = new EditorSceneService(this);
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
        Commands = new CommandService(new EditorCommandContext(_sceneService, Selection));
        Commands.CommandHistoryChanged += OnCommandHistoryChanged;
    }

    public override void _Ready()
    {
        EnsureToolManager();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        bool handled = key.Keycode == Key.Escape
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

    public void RegisterSelectionTranslationGizmo(Gizmo3D gizmo, Node3D target)
    {
        _selectionTranslationGizmoController?.Register(gizmo, target);
    }

    public void UnregisterSelectionTranslationGizmo(Gizmo3D gizmo)
    {
        _selectionTranslationGizmoController?.Unregister(gizmo);
    }

    public override void _ExitTree()
    {
        if (_toolManager != null)
        {
            _toolManager.CommandSubmitted -= Commands.Execute;
            _toolManager.PreviewSubmitted -= _previewService.Apply;
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
        _objectSelectionHighlightController?.Dispose();
        _objectSelectionHighlightController = null;
        _componentSelectionHighlightController?.Dispose();
        _componentSelectionHighlightController = null;
        _sceneService?.Dispose();
        _sceneService = null;
        Selection?.Dispose();
        Selection = null;
        Commands.CommandHistoryChanged -= OnCommandHistoryChanged;
        Commands.Dispose();
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
        _selectionTranslationGizmoController.CommandSubmitted += Commands.Execute;
        _selectionTranslationGizmoController.PreviewSubmitted += _previewService.Apply;
        _translationGizmoEventsWired = true;
    }

    private void OnCommandHistoryChanged()
    {
        _componentSelectionHighlightController.Refresh();
        _selectionTranslationGizmoController.Refresh();
    }
}
