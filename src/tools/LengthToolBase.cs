using System;
using Godot;

// Shared length setting + primary action wiring for tools that apply a world-space length.
public abstract partial class LengthToolBase
    : ToolBase,
        IEditorToolSettingsProvider,
        IEditorToolPrimaryAction
{
    public const float DefaultLength = 16.0f;
    public const float MinimumLength = 0.001f;

    private float? _length;

    protected LengthToolBase(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    protected abstract string SettingsName { get; }

    protected abstract string SettingsTitle { get; }

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

    public abstract string PrimaryActionText { get; }

    public event Action? PrimaryActionChanged;

    public Control CreateSettingsControl(IEditorToolContext context)
    {
        InitializeLength(context);
        return new SettingsControl(this);
    }

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context)
    {
        InitializeLength(context);
        return CreateLengthAction(context, Length);
    }

    protected abstract EditorToolAction CreateLengthAction(
        IEditorToolContext context,
        float length
    );

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
        private readonly LengthToolBase _tool;
        private readonly SpinBox _lengthSpin;
        private bool _syncing;

        public SettingsControl(LengthToolBase tool)
        {
            _tool = tool;
            Name = tool.SettingsName;
            AddThemeConstantOverride("separation", 8);
            AddChild(new Label { Text = tool.SettingsTitle });

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
