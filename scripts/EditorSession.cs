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
    private PrimitiveCreationTool _primitiveCreationTool;

    public override void _EnterTree()
    {
        RayPicking = new RayPickingService(GetWorld3D());
    }

    // TODO: At some point we may want a more robust way to create and wire up tools.
    public override void _Ready()
    {
        _primitiveCreationTool = new PrimitiveCreationTool(this);
    }

    public void BeginPrimitiveCreation(PrimitiveCreationSettings settings)
    {
        _primitiveCreationTool ??= new PrimitiveCreationTool(this);
        _primitiveCreationTool.Begin(settings);
    }

    public override void _ExitTree()
    {
        _primitiveCreationTool?.Dispose();
        _primitiveCreationTool = null;
        Commands.Dispose();
    }
}
