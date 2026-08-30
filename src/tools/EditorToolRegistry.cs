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

    public EditorToolRegistry(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public IEditorTool? ActiveTool => _activeTool;

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

    public void Activate(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_tools.TryGetValue(id, out IEditorTool? tool))
            throw new ArgumentException($"No tool is registered as '{id}'.", nameof(id));

        if (ReferenceEquals(_activeTool, tool))
            return;

        SetActiveTool(tool);
    }

    public void Deactivate()
    {
        if (_activeTool is null)
            return;

        SetActiveTool(null);
    }

    // Abandons the active interaction while leaving the tool active. Lifecycle transitions such as
    // undo or scene replacement use this instead of deactivating.
    public void CancelActiveTool() => _activeTool?.Cancel(_context);

    public void MouseDown(EditorMouseInput input) => _activeTool?.OnMouseDown(_context, input);

    public void MouseDrag(EditorMouseInput input) => _activeTool?.OnMouseDrag(_context, input);

    public void MouseUp(EditorMouseInput input) => _activeTool?.OnMouseUp(_context, input);

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
            _activeTool = tool;
        }
    }
}
