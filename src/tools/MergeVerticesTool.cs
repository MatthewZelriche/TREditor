using System;
using Godot;

public enum VertexMergePosition
{
    First,
    Last,
    Centroid,
}

public sealed partial class MergeVerticesTool
    : ToolBase,
        IEditorToolSettingsProvider,
        IEditorToolPrimaryAction
{
    public const string ToolId = "tool.merge_vertices";
    private VertexMergePosition _position = VertexMergePosition.Last;

    public MergeVerticesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public VertexMergePosition Position
    {
        get => _position;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_position == value)
                return;
            _position = value;
            PrimaryActionChanged?.Invoke();
        }
    }

    public event Action? PrimaryActionChanged;

    public string PrimaryActionText => "Merge Selected";

    public Control CreateSettingsControl(IEditorToolContext _) =>
        MergeVerticesSettings.Create(this);

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context) =>
        MergeVerticesCommand.CreateAction(context, Position);
}
