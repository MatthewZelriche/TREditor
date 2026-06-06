using System;
using Godot;

public sealed class EditorToolManager : IDisposable
{
    private readonly EditorToolContext _context;
    private readonly Func<PrimitiveCreationSettings> _getPrimitiveCreationSettings;
    private IEditorTool _persistentTool;
    private IEditorTool _temporaryTool;
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

    public bool HasTemporaryTool => _temporaryTool != null;

    public event Action<EditorCommand> CommandSubmitted;
    public event Action<EditorPreviewRequest> PreviewSubmitted;

    public void ActivatePersistentTool(EditorToolId toolId)
    {
        ThrowIfDisposed();

        if (_temporaryTool == null && _persistentToolId == toolId)
        {
            return;
        }

        CancelTemporaryTool(restorePersistentTool: false);

        if (_persistentToolId != toolId)
        {
            ExitPersistentTool();
            ClearPreview();
            _persistentTool = CreatePersistentTool(toolId);
            _persistentToolId = toolId;
        }

        EnterPersistentTool();
    }

    public void StartTemporaryTool(IEditorTool tool)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(tool);

        CancelTemporaryTool(restorePersistentTool: false);
        ExitPersistentTool();
        ClearPreview();

        _temporaryTool = tool;
        _temporaryTool.Enter();
    }

    public bool CancelTemporaryTool() => CancelTemporaryTool(restorePersistentTool: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeFromViewportInput();
        CancelTemporaryTool(restorePersistentTool: false);
        ExitPersistentTool();
    }

    private void OnViewportMouseButton(ViewportMouseButtonEvent input)
    {
        IEditorTool tool = ActiveTool;
        ProcessToolResult(tool, tool.HandleMouseButton(input));
    }

    private void OnViewportMouseMotion(ViewportMouseMotionEvent input)
    {
        IEditorTool tool = ActiveTool;
        ProcessToolResult(tool, tool.HandleMouseMotion(input));
    }

    private IEditorTool ActiveTool => _temporaryTool ?? _persistentTool;

    private void ProcessToolResult(IEditorTool tool, EditorToolResult result)
    {
        SubmitCommand(result.Command);
        SubmitPreview(result.Preview);

        if (result.Status == EditorToolStatus.Continue || tool != _temporaryTool)
        {
            return;
        }

        _temporaryTool = null;
        tool.Exit();
        ClearPreview();
        EnterPersistentTool();
    }

    private bool CancelTemporaryTool(bool restorePersistentTool)
    {
        if (_temporaryTool == null)
        {
            if (restorePersistentTool)
            {
                EnterPersistentTool();
            }

            return false;
        }

        IEditorTool tool = _temporaryTool;
        _temporaryTool = null;
        EditorToolResult result = tool.Cancel();
        SubmitCommand(result.Command);
        tool.Exit();
        ClearPreview();

        if (restorePersistentTool)
        {
            EnterPersistentTool();
        }

        return true;
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
        if (_persistentToolEntered || _temporaryTool != null)
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
