using System;
using System.Collections.Generic;
using Godot;

public partial class MainViewportArea : TilingPaneContainer<MainViewport>
{
    private readonly List<MainViewport> _viewports = [];
    private MainViewport? _activeViewport;

    public MainViewportArea()
        : base(static () => new MainViewport())
    {
        ChildEnteredTree += OnChildEnteredTree;
        ChildExitingTree += OnChildExitingTree;
    }

    public event Action<MainViewport?>? ActiveViewportChanged;

    public MainViewport? ActiveViewport => _activeViewport;

    public override void _Ready()
    {
        SetRootPane(CreatePane());
    }

    private void OnChildEnteredTree(Node child)
    {
        if (child is not TilingContainerPane<MainViewport> pane)
            return;

        MainViewport viewport = pane.Content;
        if (_viewports.Contains(viewport))
            return;

        _viewports.Add(viewport);
        viewport.ActivationRequested += OnViewportActivationRequested;
        if (_activeViewport is null)
            SetActiveViewport(viewport);
    }

    private void OnChildExitingTree(Node child)
    {
        if (child is not TilingContainerPane<MainViewport> pane)
            return;

        MainViewport viewport = pane.Content;
        viewport.ActivationRequested -= OnViewportActivationRequested;
        viewport.StopNavigation();
        _viewports.Remove(viewport);

        if (ReferenceEquals(_activeViewport, viewport))
            SetActiveViewport(_viewports.Count == 0 ? null : _viewports[0]);
    }

    private void OnViewportActivationRequested(MainViewport viewport) =>
        SetActiveViewport(viewport);

    private void SetActiveViewport(MainViewport? viewport)
    {
        if (ReferenceEquals(_activeViewport, viewport))
            return;

        _activeViewport?.StopNavigation();
        _activeViewport = viewport;
        ActiveViewportChanged?.Invoke(viewport);
    }
}
