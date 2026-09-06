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
    private Document? _currentDocument;
    private string? _currentFilePath;
    private bool _disposed;

    public Application(Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(mapRoot);
        var initialDocument = new Document();
        RenderService? renderService = null;
        PickingService? pickingService = null;
        TranslateGizmoCoordinator? gizmos = null;
        try
        {
            gizmos = new TranslateGizmoCoordinator(mapRoot, _selectionState, _previewState);
            renderService = new RenderService(initialDocument, mapRoot, _previewState);
            pickingService = new PickingService(initialDocument, mapRoot, _previewState);
            _services = new SceneServices(
                initialDocument,
                _previewState,
                renderService,
                pickingService,
                gizmos
            );
            _services.FatalError += OnSceneServicesFatalError;
            _toolContext = new EditorToolContext(
                () => _currentDocument,
                _commandHistory,
                pickingService,
                _selectionState,
                _previewState,
                gizmos
            );
            _toolRegistry = new EditorToolRegistry(_toolContext);
            _toolRegistry.Register(new TranslateTool());
            _toolRegistry.SetDefaultTool(TranslateTool.ToolId);
            _toolRegistry.Activate(TranslateTool.ToolId);
        }
        catch
        {
            gizmos?.Dispose();
            pickingService?.Dispose();
            renderService?.Dispose();
            initialDocument.Dispose();
            throw;
        }

        _currentDocument = initialDocument;
        _commandHistory.SetDocument(initialDocument);
        _commandHistory.Changed += OnCommandHistoryChanged;
        SetUpDocumentForDebug();
    }

    // Tool- and UI-facing document access. Only value snapshots leave this boundary.
    public IEditorDocumentReader? Document => _currentDocument;

    internal Document? CurrentDocument => _currentDocument;

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

    // Raised when a tool interaction fails so the UI can present the error and the interaction is
    // already cancelled.
    public event Action<string, Exception>? OperationFailed;

    // A scene service failed after an authoritative document operation may already have completed.
    // We must treat this as unrecoverable because it may leave the services in a partially mutated state
    // TODO: In the future we may want to protect more robustly against this kind of behavior
    public event Action<string, Exception>? FatalError;

    // Retained so Root can present a failure raised while Application is still being constructed,
    // before the UI has had an opportunity to subscribe to FatalError.
    public (string Operation, Exception Exception)? FatalFailure { get; private set; }

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
        ReplaceDocument(new Document(), null);
    }

    public void OpenScene(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Document loadedDocument = SceneSerializer.Load(path);
        ReplaceDocument(loadedDocument, path);
    }

    public void SaveScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Document document = _currentDocument
            ?? throw new InvalidOperationException("There is no scene to save.");
        string path = _currentFilePath
            ?? throw new InvalidOperationException(
                "The current scene has no file path. Use SaveSceneAs first."
            );

        SceneSerializer.Save(document, path);
    }

    public void SaveSceneAs(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Document document = _currentDocument
            ?? throw new InvalidOperationException("There is no scene to save.");

        SceneSerializer.Save(document, path);
        _currentFilePath = path;
    }

    // Viewport input arrives here and goes no further than the active tool.
    public void HandleToolMouseDown(EditorMouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RunProtectedToolOperation("Tool interaction", () => _toolRegistry.MouseDown(input));
    }

    public void HandleToolMouseDrag(EditorMouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RunProtectedToolOperation("Tool interaction", () => _toolRegistry.MouseDrag(input));
    }

    public void HandleToolMouseUp(EditorMouseInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RunProtectedToolOperation("Tool interaction", () => _toolRegistry.MouseUp(input));
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
            // Release retained commands before services or the current document are disposed so
            // disposable commands can still touch document-owned state during cleanup.
            _commandHistory.Changed -= OnCommandHistoryChanged;
            _services.FatalError -= OnSceneServicesFatalError;
            CommandHistoryChanged = null;
            FatalError = null;
            _toolRegistry.CancelActiveTool();
            _selectionMode.ClearChangedListeners();
            _selectionState.Clear();
            _selectionState.ClearChangedListeners();
            _commandHistory.Dispose();
            _services.Dispose();
        }
        finally
        {
            _currentDocument?.Dispose();
            _currentDocument = null;
            _currentFilePath = null;
        }
    }

    private void OnCommandHistoryChanged() => CommandHistoryChanged?.Invoke();

    private void OnSceneServicesFatalError(string operation, Exception exception)
    {
        FatalFailure ??= (operation, exception);
        FatalError?.Invoke(operation, exception);
    }

    private void RunProtectedToolOperation(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            CancelActiveInteraction();
            OperationFailed?.Invoke(operation, exception);
        }
    }

    private void ReplaceDocument(Document document, string? path)
    {
        Document? previousDocument = _currentDocument;
        // Nothing a tool was interacting with survives the document it belonged to.
        CancelActiveInteraction();

        try
        {
            _services.SetDocument(document);
        }
        catch
        {
            document.Dispose();
            throw;
        }

        _currentDocument = document;
        _currentFilePath = path;
        // Transient editor state cannot outlive the document it referred to.
        _selectionState.Clear();
        // Clear before disposing the previous document so discarded commands can release state that
        // still refers to it. Failures above leave the existing history intact.
        _commandHistory.Clear();
        // Retarget only after the old history is gone so a discarded command cannot reach the new document.
        _commandHistory.SetDocument(document);
        previousDocument?.Dispose();
    }

    // TODO: Delete this when Primitive Creation is hooked up
    private void SetUpDocumentForDebug()
    {
        Document document = _currentDocument
            ?? throw new InvalidOperationException("A document must exist before adding debug geometry.");
        var cubeTransform = new Transform3D(
            Basis.Identity,
            new Vector3(-1.5f, 0.0f, 0.0f)
        );
        var cylinderTransform = new Transform3D(
            Basis.Identity,
            new Vector3(1.5f, 0.0f, 0.0f)
        );

        document.Add(
            "Debug Cube",
            cubeTransform,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        document.Add(
            "Debug Cylinder",
            cylinderTransform,
            new GDMesh(SpatialMeshBuilder.CreateCylinder(2.5f, 1.75f, 16))
        );
    }
}
