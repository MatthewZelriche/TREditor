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
        new SettingsControl(this);

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context) =>
        MergeVerticesCommand.CreateAction(context, Position);

    private sealed partial class SettingsControl : VBoxContainer
    {
        private readonly MergeVerticesTool _tool;
        private readonly OptionButton _positionOption;
        private bool _syncing;

        public SettingsControl(MergeVerticesTool tool)
        {
            _tool = tool;
            AddThemeConstantOverride("separation", 8);
            AddChild(new Label { Text = "Merge Vertices" });

            _positionOption = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _positionOption.AddItem("First", (int)VertexMergePosition.First);
            _positionOption.AddItem("Last", (int)VertexMergePosition.Last);
            _positionOption.AddItem("Centroid", (int)VertexMergePosition.Centroid);
            _positionOption.ItemSelected += OnPositionSelected;
            AddChild(_positionOption);

            _tool.PrimaryActionChanged += Sync;
            Sync();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
                _tool.PrimaryActionChanged -= Sync;
        }

        private void Sync()
        {
            _syncing = true;
            _positionOption.Select((int)_tool.Position);
            _syncing = false;
        }

        private void OnPositionSelected(long index)
        {
            if (!_syncing)
                _tool.Position = (VertexMergePosition)_positionOption.GetItemId((int)index);
        }
    }
}
