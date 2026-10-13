using System;
using Godot;

public readonly record struct EditorToolAction(
    IEditorCommand? Command,
    string? UnavailableReason
);

// Sidebar/Enter action: UI and submission are central; the tool supplies label and command creation.
public interface IEditorToolPrimaryAction
{
    string PrimaryActionText { get; }

    EditorToolAction CreatePrimaryAction(IEditorToolContext context);

    // Tool-owned readiness changes only; selection/document refreshes are central.
    // Default no-op for tools with a static primary-action label.
    event Action? PrimaryActionChanged
    {
        add { }
        remove { }
    }
}

// Optional sidebar settings. Caller owns/frees the returned control when the tool deactivates.
public interface IEditorToolSettingsProvider
{
    Control CreateSettingsControl(IEditorToolContext context);
}

internal sealed partial class NumericToolSettings : VBoxContainer
{
    private readonly IEditorToolPrimaryAction _tool;
    private readonly SpinBox _spin;
    private readonly Func<double> _getValue;
    private readonly Action<double> _setValue;
    private bool _syncing;

    public NumericToolSettings(
        IEditorToolPrimaryAction tool,
        string name,
        string title,
        string fieldName,
        string fieldLabel,
        SpinBox spin,
        Func<double> getValue,
        Action<double> setValue
    )
    {
        _tool = tool;
        _spin = spin;
        _getValue = getValue;
        _setValue = setValue;
        Name = name;
        AddThemeConstantOverride("separation", 8);
        AddChild(new Label { Text = title });

        var row = new HBoxContainer { Name = $"{fieldName}Row" };
        row.AddChild(
            new Label
            {
                Text = fieldLabel,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            }
        );
        _spin.ValueChanged += OnValueChanged;
        row.AddChild(_spin);
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
        _spin.Value = _getValue();
        _syncing = false;
    }

    private void OnValueChanged(double value)
    {
        if (!_syncing)
            _setValue(value);
    }
}
