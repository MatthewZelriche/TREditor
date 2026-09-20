using System;
using System.Collections.Generic;
using Godot;

public partial class MainViewportArea : TilingPaneContainer<MainViewport>
{
    private readonly List<MainViewport> _viewports = [];
    private MainViewport? _activeViewport;
    // TODO: Not sure how I feel about having a back-reference to Application, but
    // we will leave it this way for now. There is probably a cleaner architecture possible
    // in the future.
    private Application? _application;

    public MainViewportArea()
        : base(static () => new MainViewport())
    {
        ChildEnteredTree += OnChildEnteredTree;
        ChildExitingTree += OnChildExitingTree;
    }

    public event Action<MainViewport?>? ActiveViewportChanged;

    public MainViewport? ActiveViewport => _activeViewport;

    // Tool input from every pane reaches the same application, which decides what the active tool
    // does with it. The area stays unaware of individual tools.
    public void Bind(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
        // Panes created before the binding still need their tool overlays.
        foreach (MainViewport viewport in _viewports)
            application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
    }

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
        viewport.ToolMouseDown += OnToolMouseDown;
        viewport.ToolMouseDrag += OnToolMouseDrag;
        viewport.ToolMouseMove += OnToolMouseMove;
        viewport.ToolMouseUp += OnToolMouseUp;
        viewport.ToolInteractionCancelled += OnToolInteractionCancelled;
        _application?.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
        if (_activeViewport is null)
            SetActiveViewport(viewport);
    }

    private void OnChildExitingTree(Node child)
    {
        if (child is not TilingContainerPane<MainViewport> pane)
            return;

        MainViewport viewport = pane.Content;
        viewport.ActivationRequested -= OnViewportActivationRequested;
        viewport.ToolMouseDown -= OnToolMouseDown;
        viewport.ToolMouseDrag -= OnToolMouseDrag;
        viewport.ToolMouseMove -= OnToolMouseMove;
        viewport.ToolMouseUp -= OnToolMouseUp;
        viewport.StopNavigation();
        viewport.StopToolDrag();
        viewport.ToolInteractionCancelled -= OnToolInteractionCancelled;
        _application?.UnregisterToolViewport(viewport.Camera);
        _viewports.Remove(viewport);

        if (ReferenceEquals(_activeViewport, viewport))
            SetActiveViewport(_viewports.Count == 0 ? null : _viewports[0]);
    }

    private void OnViewportActivationRequested(MainViewport viewport) =>
        SetActiveViewport(viewport);

    private void OnToolMouseDown(EditorMouseInput input) =>
        _application?.HandleToolMouseDown(input);

    private void OnToolMouseDrag(EditorMouseInput input) =>
        _application?.HandleToolMouseDrag(input);

    private void OnToolMouseMove(EditorMouseInput input) =>
        _application?.HandleToolMouseMove(input);

    private void OnToolMouseUp(EditorMouseInput input) =>
        _application?.HandleToolMouseUp(input);

    private void OnToolInteractionCancelled() => _application?.CancelActiveInteraction();

    private void SetActiveViewport(MainViewport? viewport)
    {
        if (ReferenceEquals(_activeViewport, viewport))
            return;

        _activeViewport?.StopNavigation();
        _activeViewport = viewport;
        ActiveViewportChanged?.Invoke(viewport);
    }
}
