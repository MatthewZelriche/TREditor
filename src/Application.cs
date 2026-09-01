using System;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

public sealed class Application : IDisposable
{
    private readonly SceneServices _services;
    private readonly EditorToolContext _toolContext;
    private readonly EditorToolRegistry _toolRegistry;
    private readonly CommandHistory _commandHistory = new();
    private readonly SelectionModeState _selectionMode = new();
    private readonly SelectionState _selectionState = new();
    private readonly PreviewState _previewState = new();
    private Scene? _currentScene;
    private string? _currentFilePath;
    private bool _disposed;

    public Application(Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(mapRoot);
        var initialScene = new Scene();
        RenderService? renderService = null;
        PickingService? pickingService = null;
        TranslateGizmoCoordinator? gizmos = null;
        try
        {
            gizmos = new TranslateGizmoCoordinator(mapRoot, _selectionState, _previewState);
            renderService = new RenderService(initialScene, mapRoot, _previewState);
            pickingService = new PickingService(initialScene, mapRoot, _previewState);
            _services = new SceneServices(
                initialScene,
                _previewState,
                renderService,
                pickingService,
                gizmos
            );
            _toolContext = new EditorToolContext(
                () => _currentScene,
                _commandHistory,
                pickingService,
                _selectionState,
                _previewState,
                gizmos
            );
            _toolRegistry = new EditorToolRegistry(_toolContext);
        }
        catch
        {
            gizmos?.Dispose();
            pickingService?.Dispose();
            renderService?.Dispose();
            initialScene.Dispose();
            throw;
        }

        _currentScene = initialScene;
        _commandHistory.SetScene(initialScene);
        _commandHistory.Changed += OnCommandHistoryChanged;
        SetUpSceneForDebug();
    }

    // Tool- and UI-facing document access. Only value snapshots leave this boundary.
    public IEditorDocumentReader? Document => _currentScene;

    internal Scene? CurrentScene => _currentScene;

    public IEditorToolContext ToolContext => _toolContext;

    // The one place tools are registered and activated. UI binds to the registry, not to tool types.
    public EditorToolRegistry Tools => _toolRegistry;

    // The editor-owned translate gizmos. Viewports register with it and the translate tool drives it;
    // nothing else needs to know a gizmo library is involved.
    internal TranslateGizmoCoordinator Gizmos => _toolContext.TranslateGizmos;

    internal SelectionState Selection => _selectionState;

    // The only production route from authoring code to the document.
    public IEditorCommandSink Commands => _commandHistory;

    public string? CurrentFilePath => _currentFilePath;

    public SelectionModeState SelectionMode => _selectionMode;

    public bool CanUndo => _commandHistory.CanUndo;

    public bool CanRedo => _commandHistory.CanRedo;

    public string? UndoDisplayName => _commandHistory.UndoDisplayName;

    public string? RedoDisplayName => _commandHistory.RedoDisplayName;

    // Raised after a successful execute, undo, redo, or a non-empty clear of command history.
    public event Action? CommandHistoryChanged;

    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelActiveInteraction();
        return _commandHistory.Undo();
    }

    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelActiveInteraction();
        return _commandHistory.Redo();
    }

    // Registers a viewport's camera and 3D overlay root so it gets its own gizmos. The viewport area
    // calls this as panes enter and leave; neither it nor the pane knows which overlays appear.
    internal void RegisterToolViewport(Camera3D camera, Node3D toolOverlayRoot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _toolContext.TranslateGizmos.RegisterViewport(camera, toolOverlayRoot);
    }

    internal void UnregisterToolViewport(Camera3D camera)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _toolContext.TranslateGizmos.UnregisterViewport(camera);
    }

    // Abandons whatever interaction is in flight without committing. Used when the pointer is taken
    // away from a tool and before history operations replace what the tool was working on. Cleaning
    // up gizmos and previews belongs to the tool that started using them, so this only cancels.
    internal void CancelActiveInteraction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _toolRegistry.CancelActiveTool();
    }

    public void NewScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReplaceScene(new Scene(), null);
    }

    public void OpenScene(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Scene loadedScene = SceneSerializer.Load(path);
        ReplaceScene(loadedScene, path);
    }

    public void SaveScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Scene scene = _currentScene
            ?? throw new InvalidOperationException("There is no scene to save.");
        string path = _currentFilePath
            ?? throw new InvalidOperationException(
                "The current scene has no file path. Use SaveSceneAs first."
            );

        SceneSerializer.Save(scene, path);
    }

    public void SaveSceneAs(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Scene scene = _currentScene
            ?? throw new InvalidOperationException("There is no scene to save.");

        SceneSerializer.Save(scene, path);
        _currentFilePath = path;
    }

    // Viewport input arrives here and goes no further than the active tool.
    public void HandleToolMouseDown(EditorMouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _toolRegistry.MouseDown(input);
    }

    public void HandleToolMouseDrag(EditorMouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _toolRegistry.MouseDrag(input);
    }

    public void HandleToolMouseUp(EditorMouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _toolRegistry.MouseUp(input);
    }

    public void Update(double delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = delta;
        _services.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            // Release retained commands before services or the current scene are disposed so
            // disposable commands can still touch scene-owned state during cleanup.
            _commandHistory.Changed -= OnCommandHistoryChanged;
            CommandHistoryChanged = null;
            _toolRegistry.CancelActiveTool();
            _selectionMode.ClearChangedListeners();
            _selectionState.Clear();
            _selectionState.ClearChangedListeners();
            _commandHistory.Dispose();
            _services.Dispose();
        }
        finally
        {
            _currentScene?.Dispose();
            _currentScene = null;
            _currentFilePath = null;
        }
    }

    private void OnCommandHistoryChanged() => CommandHistoryChanged?.Invoke();

    private void ReplaceScene(Scene scene, string? path)
    {
        Scene? previousScene = _currentScene;
        // Nothing a tool was interacting with survives the document it belonged to.
        CancelActiveInteraction();

        try
        {
            _services.SetScene(scene);
        }
        catch
        {
            scene.Dispose();
            throw;
        }

        _currentScene = scene;
        _currentFilePath = path;
        // Transient editor state cannot outlive the document it referred to.
        _selectionState.Clear();
        // Clear before disposing the previous scene so discarded commands can release state that
        // still refers to it. Failures above leave the existing history intact.
        _commandHistory.Clear();
        // Retarget only after the old history is gone so a discarded command cannot reach the new scene.
        _commandHistory.SetScene(scene);
        previousScene?.Dispose();
    }

    // TODO: Delete this when Primitive Creation is hooked up
    private void SetUpSceneForDebug()
    {
        Scene scene = _currentScene
            ?? throw new InvalidOperationException("A scene must exist before adding debug geometry.");
        var cubeTransform = new Transform3D(
            Basis.Identity,
            new Vector3(-1.5f, 0.0f, 0.0f)
        );
        var cylinderTransform = new Transform3D(
            Basis.Identity,
            new Vector3(1.5f, 0.0f, 0.0f)
        );

        scene.Add(
            "Debug Cube",
            cubeTransform,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        scene.Add(
            "Debug Cylinder",
            cylinderTransform,
            new GDMesh(SpatialMeshBuilder.CreateCylinder(2.5f, 1.75f, 16))
        );
    }
}
