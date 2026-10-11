using System;
using Godot;

public sealed partial class ExtendEdgesTool
    : ToolBase,
        IEditorToolSettingsProvider,
        IEditorToolPrimaryAction
{
    public const string ToolId = "tool.extend_edges";
    public const float DefaultLength = 16.0f;
    public const float MinimumLength = 0.001f;

    private float? _length;

    public ExtendEdgesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public float Length
    {
        get => _length ?? DefaultLength;
        set
        {
            if (!float.IsFinite(value) || value < MinimumLength)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_length == value)
                return;

            _length = value;
            PrimaryActionChanged?.Invoke();
        }
    }

    public string PrimaryActionText => "Extend Selected";

    public event Action? PrimaryActionChanged;

    public Control CreateSettingsControl(IEditorToolContext context)
    {
        InitializeLength(context);
        return new SettingsControl(this);
    }

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context)
    {
        InitializeLength(context);
        return ExtendEdgesCommand.CreateAction(context, Length);
    }

    private void InitializeLength(IEditorToolContext context)
    {
        if (_length is not null)
            return;

        _length =
            context.Snap.GridSnap is float snap
            && float.IsFinite(snap)
            && snap >= MinimumLength
                ? snap
                : DefaultLength;
    }

    private sealed partial class SettingsControl : VBoxContainer
    {
        private readonly ExtendEdgesTool _tool;
        private readonly SpinBox _lengthSpin;
        private bool _syncing;

        public SettingsControl(ExtendEdgesTool tool)
        {
            _tool = tool;
            Name = "ExtendEdgesSettings";
            AddThemeConstantOverride("separation", 8);
            AddChild(new Label { Text = "Extend Edges" });

            var row = new HBoxContainer { Name = "LengthRow" };
            row.AddChild(
                new Label
                {
                    Text = "Length",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                }
            );
            _lengthSpin = new SpinBox
            {
                Name = "LengthSpin",
                MinValue = MinimumLength,
                MaxValue = 1000000.0,
                Step = MinimumLength,
                AllowGreater = true,
                AllowLesser = false,
            };
            _lengthSpin.ValueChanged += OnLengthChanged;
            row.AddChild(_lengthSpin);
            AddChild(row);

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
            _lengthSpin.Value = _tool.Length;
            _syncing = false;
        }

        private void OnLengthChanged(double value)
        {
            if (!_syncing)
                _tool.Length = (float)value;
        }
    }
}
