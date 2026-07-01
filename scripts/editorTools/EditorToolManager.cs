using System;
using Godot;

public sealed class EditorToolManager : IDisposable
{
    private readonly EditorToolContext _context;
    private readonly Func<PrimitiveCreationSettings> _getPrimitiveCreationSettings;
    private IEditorTool _persistentTool;
    private EditorToolId _persistentToolId;
    private bool _persistentToolEntered;
    private bool _subscribedToViewportInput;
    private bool _disposed;

    public EditorToolManager(
        EditorToolContext context,
        Func<PrimitiveCreationSettings> getPrimitiveCreationSettings
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(getPrimitiveCreationSettings);

        _persistentToolId = EditorToolId.Select;
        _context = context;
        _getPrimitiveCreationSettings = getPrimitiveCreationSettings;
        _persistentTool = CreatePersistentTool(_persistentToolId);

        SubscribeToViewportInput();
        EnterPersistentTool();
    }

    public EditorToolId PersistentToolId => _persistentToolId;

    public event Action<EditorCommand> CommandSubmitted;
    public event Action<EditorPreviewRequest> PreviewSubmitted;
    public event Action<EditorToolId> PersistentToolChanged;

    public void ActivatePersistentTool(EditorToolId toolId)
    {
        ThrowIfDisposed();

        if (_persistentToolId == toolId)
        {
            return;
        }

        ExitPersistentTool();
        ClearPreview();
        _persistentTool = CreatePersistentTool(toolId);
        _persistentToolId = toolId;
        PersistentToolChanged?.Invoke(toolId);
        EnterPersistentTool();
    }

    public bool HandleAction(EditorInputAction action)
    {
        ThrowIfDisposed();

        EditorToolResult result = _persistentTool.HandleAction(action);
        ProcessToolResult(result);
        return result.Command != null
            || result.Preview != null
            || result.Status != EditorToolStatus.Continue;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeFromViewportInput();
        ExitPersistentTool();
    }

    private void OnViewportMouseButton(ViewportMouseButtonEvent input)
    {
        ProcessToolResult(_persistentTool.HandleMouseButton(input));
    }

    private void OnViewportMouseMotion(ViewportMouseMotionEvent input)
    {
        ProcessToolResult(_persistentTool.HandleMouseMotion(input));
    }

    private void ProcessToolResult(EditorToolResult result)
    {
        SubmitCommand(result.Command);
        SubmitPreview(result.Preview);
    }

    private void SubmitCommand(EditorCommand command)
    {
        if (command == null)
        {
            return;
        }

        CommandSubmitted?.Invoke(command);
    }

    private void SubmitPreview(EditorPreviewRequest request)
    {
        if (request == null)
        {
            return;
        }

        PreviewSubmitted?.Invoke(request);
    }

    private void ClearPreview()
    {
        PreviewSubmitted?.Invoke(new EditorPreviewRequest.Clear());
    }

    private void EnterPersistentTool()
    {
        if (_persistentToolEntered)
        {
            return;
        }

        _persistentTool.Enter();
        _persistentToolEntered = true;
    }

    private void ExitPersistentTool()
    {
        if (!_persistentToolEntered)
        {
            return;
        }

        _persistentTool.Exit();
        _persistentToolEntered = false;
    }

    private IEditorTool CreatePersistentTool(EditorToolId toolId)
    {
        return toolId switch
        {
            EditorToolId.Select => new SelectTool(_context),
            EditorToolId.Edit => new EditTool(_context),
            EditorToolId.Create => new PrimitiveCreationTool(
                _getPrimitiveCreationSettings,
                _context
            ),
            EditorToolId.Texture => new TextureTool(_context),
            _ => throw new ArgumentOutOfRangeException(nameof(toolId), toolId, null),
        };
    }

    private void SubscribeToViewportInput()
    {
        if (ViewportInputEvents.Instance == null)
        {
            GD.PushWarning("EditorToolManager could not find ViewportInputEvents.");
            return;
        }

        ViewportInputEvents.Instance.ViewportMouseButton += OnViewportMouseButton;
        ViewportInputEvents.Instance.ViewportMouseMotion += OnViewportMouseMotion;
        _subscribedToViewportInput = true;
    }

    private void UnsubscribeFromViewportInput()
    {
        if (!_subscribedToViewportInput || ViewportInputEvents.Instance == null)
        {
            return;
        }

        ViewportInputEvents.Instance.ViewportMouseButton -= OnViewportMouseButton;
        ViewportInputEvents.Instance.ViewportMouseMotion -= OnViewportMouseMotion;
        _subscribedToViewportInput = false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
