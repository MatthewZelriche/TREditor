using System;

// Mutually exclusive Object / Component editor modes, plus the active component-type mask.
// "All" and explicit Vertex/Edge/Face combinations are mutually exclusive UI paths that both
// use Component mode; IsAllComponents distinguishes them.
public sealed class SelectionModeState
{
    private static readonly ComponentMask SingleComponents =
        ComponentMask.Vertex | ComponentMask.Edge | ComponentMask.Face;

    public EditorMode Mode { get; private set; } = EditorMode.Object;

    public ComponentMask Components { get; private set; } = ComponentMask.None;

    public bool XRayEnabled { get; private set; }

    // True when Component mode was entered via the All path. False when any explicit
    // Vertex/Edge/Face combination is active, including all three bits together.
    public bool IsAllComponents { get; private set; }

    public event Action? Changed;

    public void SetXRayEnabled(bool enabled)
    {
        if (XRayEnabled == enabled)
            return;

        XRayEnabled = enabled;
        Changed?.Invoke();
    }

    public void EnterObjectMode()
    {
        SetState(EditorMode.Object, ComponentMask.None, isAllComponents: false);
    }

    public void SelectAllComponents()
    {
        SetState(EditorMode.Component, ComponentMask.All, isAllComponents: true);
    }

    // Toggles a single Vertex, Edge, or Face bit. Leaves Object mode / All mode for an explicit
    // combination. Refuses to clear the last remaining component bit.
    public void ToggleComponent(ComponentMask component)
    {
        if (!IsSingleComponent(component))
        {
            throw new ArgumentOutOfRangeException(
                nameof(component),
                component,
                "Expected a single Vertex, Edge, or Face flag."
            );
        }

        if (Mode != EditorMode.Component || IsAllComponents)
        {
            SetState(EditorMode.Component, component, isAllComponents: false);
            return;
        }

        ComponentMask next = Components.HasFlag(component)
            ? Components & ~component
            : Components | component;

        if (next == ComponentMask.None)
            return;

        SetState(EditorMode.Component, next, isAllComponents: false);
    }

    public void ClearChangedListeners() => Changed = null;

    private void SetState(EditorMode mode, ComponentMask components, bool isAllComponents)
    {
        if (Mode == mode && Components == components && IsAllComponents == isAllComponents)
            return;

        Mode = mode;
        Components = components;
        IsAllComponents = isAllComponents;
        Changed?.Invoke();
    }

    private static bool IsSingleComponent(ComponentMask component)
    {
        int value = (int)component;
        return value != 0
            && (value & (int)SingleComponents) == value
            && (value & (value - 1)) == 0;
    }
}
