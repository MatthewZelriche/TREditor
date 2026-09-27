using System;

// Single-item selection. Selection is transient editor state and is never serialized, but user-driven
// changes go through SelectionCommand so they undo and redo with the rest of the history. The direct
// Select and Clear calls remain for application lifecycle cleanup such as New, Open, and disposal.
public sealed class SelectionState
{
    public SelectionValue Current { get; private set; }

    public event Action? Changed;

    public void Select(MeshObjectId id) => SetSelected(SelectionValue.ForObject(id));

    public void Select(MeshComponentId component) =>
        SetSelected(SelectionValue.ForComponent(component));

    public void Clear() => SetSelected(SelectionValue.Empty);

    public void ClearChangedListeners() => Changed = null;

    // The route SelectionCommand uses so it can restore an object, component, or empty selection.
    internal void Apply(SelectionValue selection) => SetSelected(selection);

    private void SetSelected(SelectionValue selection)
    {
        if (Current == selection)
            return;

        Current = selection;
        Changed?.Invoke();
    }
}
