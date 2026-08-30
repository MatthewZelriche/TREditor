using System;

// Single-object selection, deliberately stubbed for the first tool slice: tools change it directly
// on mouse down and it is neither serialized nor undoable. Selection is meant to become a
// SelectCommand recorded in command history, so treat direct Select/Clear calls as temporary.
public sealed class SelectionState
{
    public MeshObjectId? Selected { get; private set; }

    public event Action? Changed;

    public void Select(MeshObjectId id) => SetSelected(id);

    public void Clear() => SetSelected(null);

    public void ClearChangedListeners() => Changed = null;

    private void SetSelected(MeshObjectId? id)
    {
        if (Selected == id)
            return;

        Selected = id;
        Changed?.Invoke();
    }
}
