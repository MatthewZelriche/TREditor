using System;
using System.Collections.Generic;

// The single owner of the registered tools and of which one receives viewport input. Viewports and
// the surrounding UI reach tools only through this registry, so no caller needs to know a concrete
// tool type.
public sealed class EditorToolRegistry
{
    private readonly Dictionary<string, IEditorTool> _tools = new(StringComparer.Ordinal);
    private readonly IEditorToolContext _context;
    private IEditorTool? _activeTool;
    private string? _defaultToolId;

    public EditorToolRegistry(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _context.Selection.Changed += RefreshPrimaryAction;
    }

    public event Action? ActiveToolChanged;

    public event Action? PrimaryActionChanged;

    public IEditorTool? ActiveTool => _activeTool;

    public IEditorToolContext Context => _context;

    public bool HasPrimaryAction => _activeTool is IEditorToolPrimaryAction;

    public void Register(IEditorTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(tool.Id))
            throw new ArgumentException("A tool must have an identifier.", nameof(tool));

        if (!_tools.TryAdd(tool.Id, tool))
        {
            throw new ArgumentException(
                $"A tool is already registered as '{tool.Id}'.",
                nameof(tool)
            );
        }
    }

    // The tool restored whenever activation is cleared. Call after the default tool is registered.
    public void SetDefaultTool(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_tools.ContainsKey(id))
            throw new ArgumentException($"No tool is registered as '{id}'.", nameof(id));

        _defaultToolId = id;
    }

    public void Activate(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_tools.TryGetValue(id, out IEditorTool? tool))
            throw new ArgumentException($"No tool is registered as '{id}'.", nameof(id));

        if (ReferenceEquals(_activeTool, tool))
            return;

        SetActiveTool(tool);
    }

    // Returns to the default tool when one is set; otherwise clears the active tool.
    public void Deactivate()
    {
        if (_defaultToolId is not null)
        {
            Activate(_defaultToolId);
            return;
        }

        if (_activeTool is null)
            return;

        SetActiveTool(null);
    }

    // Abandons the active interaction while leaving the tool active. Lifecycle transitions such as
    // undo or scene replacement use this instead of deactivating.
    public void CancelActiveTool() => _activeTool?.Cancel(_context);

    public void MouseDown(EditorMouseInput input) => _activeTool?.OnMouseDown(_context, input);

    public void MouseDrag(EditorMouseInput input) => _activeTool?.OnMouseDrag(_context, input);

    public void MouseMove(EditorMouseInput input) => _activeTool?.OnMouseMove(_context, input);

    public void MouseUp(EditorMouseInput input) => _activeTool?.OnMouseUp(_context, input);

    // Null on success or when the active tool has no primary action; otherwise the warning to show.
    public string? TryExecutePrimaryAction()
    {
        if (_activeTool is not IEditorToolPrimaryAction action)
            return null;

        EditorToolAction primaryAction = action.CreatePrimaryAction(_context);
        if (primaryAction.Command is not { } command)
            return primaryAction.UnavailableReason;

        return _context.Commands.TrySubmit(command, out string? failure) ? null : failure;
    }

    internal void RefreshPrimaryAction() => PrimaryActionChanged?.Invoke();

    private void SetActiveTool(IEditorTool? tool)
    {
        IEditorTool? previousTool = _activeTool;
        try
        {
            previousTool?.Cancel(_context);
        }
        finally
        {
            // The switch completes even when the outgoing tool fails to cancel; leaving a tool that
            // already threw in charge of input would be worse than losing its cleanup.
            if (previousTool is IEditorToolPrimaryAction previousAction)
                previousAction.PrimaryActionChanged -= RefreshPrimaryAction;
            _activeTool = tool;
            if (tool is IEditorToolPrimaryAction action)
                action.PrimaryActionChanged += RefreshPrimaryAction;
            ActiveToolChanged?.Invoke();
        }
    }
}
