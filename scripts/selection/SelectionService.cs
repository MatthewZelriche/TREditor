using System;

public sealed class SelectionService
{
    public event Action<SelectionSnapshot> SelectionChanged;

    public SelectionSnapshot Current { get; private set; } = SelectionSnapshot.Empty;

    public bool Contains(SelectionTarget target) => Current.Contains(target);

    public bool Apply(SelectionSnapshot selection)
    {
        if (Current == selection)
            return false;

        Current = selection;
        SelectionChanged?.Invoke(selection);
        return true;
    }
}
