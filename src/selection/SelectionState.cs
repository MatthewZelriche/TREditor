using System;

// Multi-item selection. Selection is transient editor state and is never serialized, but user-driven
// changes go through SelectionCommand so they undo and redo with the rest of the history. The direct
// Select and Clear calls remain for application lifecycle cleanup such as New, Open, and disposal.
public sealed class SelectionState
{
    public SelectionValue Current { get; private set; }

    public MeshComponentId? HoveredComponent { get; private set; }

    public event Action? Changed;

    // Hover is deliberately separate from Changed: it is transient pointer feedback and must not
    // wake selection consumers such as transform gizmos or participate in command history.
    public event Action? HoverChanged;

    public void Select(MeshObjectId id) => SetSelected(SelectionValue.ForObject(id));

    public void Select(MeshComponentId component) =>
        SetSelected(SelectionValue.ForComponent(component));

    public void Toggle(MeshObjectId id) => SetSelected(Current.Toggle(id));

    public void Toggle(MeshComponentId component) => SetSelected(Current.Toggle(component));

    public void Clear() => SetSelected(SelectionValue.Empty);

    public void ClearChangedListeners()
    {
        Changed = null;
        HoverChanged = null;
    }

    internal void SetHoveredComponent(MeshComponentId? component)
    {
        if (HoveredComponent == component)
            return;

        HoveredComponent = component;
        HoverChanged?.Invoke();
    }

    internal void ClearHover() => SetHoveredComponent(null);

    // The route SelectionCommand uses so it can restore the complete mixed selection.
    internal void Apply(SelectionValue selection) => SetSelected(selection);

    private void SetSelected(SelectionValue selection)
    {
        if (Current == selection)
            return;

        Current = selection;
        Changed?.Invoke();
    }
}
