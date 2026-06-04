using Godot;

public partial class EditorSession : Node3D
{
    public CommandService Commands { get; } = new();

    public RayPickingService RayPicking { get; private set; }

    public float GridSnapSize
    {
        get => _gridSnapSize;
        set => _gridSnapSize = Mathf.Max(GridSnap.Off, value);
    }

    private float _gridSnapSize = GridSnap.Off;
    private EditorToolContext _toolContext;
    private EditorToolManager _toolManager;

    public override void _EnterTree()
    {
        RayPicking = new RayPickingService(GetWorld3D());
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
        }

        _toolManager?.Dispose();
        _toolManager = null;
        Commands.Dispose();
    }

    private void EnsureToolManager()
    {
        if (_toolManager != null)
        {
            return;
        }

        _toolContext = new EditorToolContext(RayPicking, this, () => GridSnapSize);
        _toolManager = new EditorToolManager(_toolContext);
        _toolManager.CommandSubmitted += Commands.Execute;
    }
}
