using System;
using Godot;

// Shared length setting + primary action wiring for tools that apply a world-space length.
// TODO: Don't like this class, will have to think of something else for this. 
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
        return new NumericToolSettings(
            this,
            SettingsName,
            SettingsTitle,
            "Length",
            "Length",
            new SpinBox
            {
                Name = "LengthSpin",
                MinValue = MinimumLength,
                MaxValue = 1000000.0,
                Step = MinimumLength,
                AllowGreater = true,
                AllowLesser = false,
            },
            () => Length,
            value => Length = (float)value
        );
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

}
