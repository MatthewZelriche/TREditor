using Godot;

public sealed partial class SelectionService : GodotObject
{
    [Signal]
    public delegate void SelectionChangedEventHandler();

    public SelectionSnapshot Current { get; private set; } = SelectionSnapshot.Empty;

    public bool Contains(SelectionTarget target) => Current.Contains(target);

    public bool Apply(SelectionSnapshot selection)
    {
        if (Current == selection)
            return false;

        Current = selection;
        EmitSignal(SignalName.SelectionChanged);
        return true;
    }
}
