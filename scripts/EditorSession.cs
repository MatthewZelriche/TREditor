using Godot;

public partial class EditorSession : Node3D
{
    public CommandService Commands { get; private set; }

    public SelectionService Selection { get; private set; }

    public ScenePickingService ScenePicking { get; private set; }

    public float GridSnapSize
    {
        get => _gridSnapSize;
        set => _gridSnapSize = Mathf.Max(GridSnap.Off, value);
    }

    private float _gridSnapSize = GridSnap.Off;
    private EditorToolContext _toolContext;
    private EditorToolManager _toolManager;
    private EditorPreviewService _previewService;
    private ObjectSelectionHighlightController _objectSelectionHighlightController;

    // Refactor opportunity: this Edit-mode view controller may eventually move closer to EditTool
    // once tool ownership/lifetime settles.
    private ComponentSelectionHighlightController _componentSelectionHighlightController;
    private EditorSceneService _sceneService;

    public override void _EnterTree()
    {
        ScenePicking = new ScenePickingService(GetWorld3D());
        Selection = new SelectionService();
        _sceneService = new EditorSceneService(this);
        _objectSelectionHighlightController = new ObjectSelectionHighlightController(
            _sceneService,
            Selection
        );
        _componentSelectionHighlightController = new ComponentSelectionHighlightController(
            _sceneService,
            Selection
        );
        Commands = new CommandService(new EditorCommandContext(_sceneService, Selection));
    }

    public override void _Ready()
    {
        EnsureToolManager();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (
            @event is InputEventKey { Pressed: true, Echo: false } key
            && key.Keycode == Key.Escape
            && _toolManager?.CancelTemporaryTool() == true
        )
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public void ActivatePersistentTool(EditorToolId toolId)
    {
        EnsureToolManager();
        _toolManager.ActivatePersistentTool(toolId);
    }

    public void BeginPrimitiveCreation(PrimitiveCreationSettings settings)
    {
        EnsureToolManager();
        _toolManager.StartTemporaryTool(new PrimitiveCreationTool(settings, _toolContext));
    }

    public override void _ExitTree()
    {
        if (_toolManager != null)
        {
            _toolManager.CommandSubmitted -= Commands.Execute;
            _toolManager.PreviewSubmitted -= _previewService.Apply;
        }

        _toolManager?.Dispose();
        _toolManager = null;
        _previewService?.Dispose();
        _previewService = null;
        _objectSelectionHighlightController?.Dispose();
        _objectSelectionHighlightController = null;
        _componentSelectionHighlightController?.Dispose();
        _componentSelectionHighlightController = null;
        _sceneService?.Dispose();
        _sceneService = null;
        Selection?.Dispose();
        Selection = null;
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
            () => GridSnapSize
        );
        _previewService = new EditorPreviewService(this);
        _toolManager = new EditorToolManager(_toolContext);
        _toolManager.CommandSubmitted += Commands.Execute;
        _toolManager.PreviewSubmitted += _previewService.Apply;
    }
}
