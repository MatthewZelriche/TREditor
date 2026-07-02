using System;

/// <summary>
/// Provides shared editor dependencies when a command is applied or undone. Commands can retain
/// only their operation data while <see cref="CommandService"/> supplies lifecycle, mesh
/// operations, and selection access at execution time.
/// </summary>
public sealed class EditorCommandContext
{
    private readonly Func<SelectionSnapshot, bool> _applySelection;

    public EditorCommandContext(
        EditorObjectLifecycle lifecycle,
        EditorMeshOperations operations,
        SelectionService selection
    )
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(selection);

        Lifecycle = lifecycle;
        Operations = operations;
        _applySelection = selection.Apply;
    }

    internal EditorCommandContext(
        EditorObjectLifecycle lifecycle,
        EditorMeshOperations operations,
        Func<SelectionSnapshot, bool> applySelection
    )
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(applySelection);

        Lifecycle = lifecycle;
        Operations = operations;
        _applySelection = applySelection;
    }

    public EditorObjectLifecycle Lifecycle { get; }

    public EditorMeshOperations Operations { get; }

    public bool ApplySelection(SelectionSnapshot selection) => _applySelection(selection);
}
