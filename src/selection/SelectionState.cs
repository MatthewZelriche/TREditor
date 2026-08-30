using System;

// Single-object selection. Selection is transient editor state and is never serialized, but user-driven
// changes go through SelectionCommand so they undo and redo with the rest of the history. The direct
// Select and Clear calls remain for application lifecycle cleanup such as New, Open, and disposal.
public sealed class SelectionState
{
    public MeshObjectId? Selected { get; private set; }

    public event Action? Changed;

    public void Select(MeshObjectId id) => SetSelected(id);

    public void Clear() => SetSelected(null);

    public void ClearChangedListeners() => Changed = null;

    // The route SelectionCommand uses so it can restore either an object or an empty selection.
    // TODO: This will change in the future as we expand on the complexity of what SelectionState is capable
    // of holding - eg multiple selections, different type of selections (component vs object), etc.
    internal void Apply(MeshObjectId? id) => SetSelected(id);

    private void SetSelected(MeshObjectId? id)
    {
        if (Selected == id)
            return;

        Selected = id;
        Changed?.Invoke();
    }
}
