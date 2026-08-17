#nullable enable

using Godot;

public partial class MainViewportArea : TilingPaneContainer<MainViewport>
{
    private bool _isInitialized;

    public MainViewportArea()
        : base(static () => new MainViewport()) { }

    public override void _Ready()
    {
        if (_isInitialized)
        {
            return;
        }

        SetRootPane(CreatePane());
        _isInitialized = true;
    }
}
