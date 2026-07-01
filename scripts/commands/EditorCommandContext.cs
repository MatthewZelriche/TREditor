using System;

/// <summary>
/// Provides shared editor dependencies when a command is applied or undone. Commands can retain
/// only their operation data while <see cref="CommandService"/> supplies scene, selection, and
/// object-lifecycle access at execution time, avoiding global service lookups and enabling focused
/// lifecycle tests.
/// </summary>
public sealed class EditorCommandContext
{
    private readonly Func<SelectionSnapshot, bool> _applySelection;

    public EditorCommandContext(EditorSceneService scene, SelectionService selection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);

        Scene = scene;
        Objects = scene;
        _applySelection = selection.Apply;
    }

    internal EditorCommandContext(
        IEditorObjectLifecycle objects,
        Func<SelectionSnapshot, bool> applySelection
    )
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(applySelection);

        Objects = objects;
        _applySelection = applySelection;
    }

    public EditorSceneService Scene { get; }

    internal IEditorObjectLifecycle Objects { get; }

    public bool ApplySelection(SelectionSnapshot selection) => _applySelection(selection);
}
