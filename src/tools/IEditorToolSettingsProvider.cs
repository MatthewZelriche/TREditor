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
