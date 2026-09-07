using System;
using Godot;

// Moves the selected object with the viewport gizmos. The tool owns the three-phase flow only: the
// coordinator owns the gizmo nodes, previews carry the drag feedback, and the document changes once,
// on mouse up, through a single command that replaces the MeshObject record captured at drag start.
public sealed class TranslateTool : IEditorTool
{
    public const string ToolId = "tool.translate";

    private bool _dragging;
    private MeshObject? _before;
    private Camera3D? _sourceCamera;

    public string Id => ToolId;

    // A press is either a gizmo grab or a selection click, never both. Classifying it before any
    // state changes keeps a selection click from starting a translation of the object it just
    // selected.
    // TODO: Base Tool class that handles selection for all tools
    public void OnMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (input.Camera is null)
            return;

        if (_dragging)
            Cancel(context);

        MeshObjectId? selected = context.Selection.Selected;
        if (
            selected is MeshObjectId id
            && context.Document.TryGetObject(id, out MeshObject before)
            && context.TranslateGizmos.BeginInteraction(input.Camera, input.ScreenPosition)
        )
        {
            _dragging = true;
            _before = before;
            _sourceCamera = input.Camera;
            return;
        }

        MeshObjectId? picked = context.Picking.Raycast(input.Camera, input.ScreenPosition);
        if (picked != selected)
            context.Commands.Submit(new SelectionCommand(context.Selection, selected, picked));
    }

    public void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        if (!context.TranslateGizmos.UpdateInteraction(input.Camera, input.ScreenPosition))
            return;

        // The proxy is the only thing the gizmo moved; showing it is a preview, not a document edit.
        context.Preview.SetTransform(_before.Id, context.TranslateGizmos.ProxyTransform);
    }

    public void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        MeshObject before = _before;
        try
        {
            context.TranslateGizmos.EndInteraction();
            MeshObject after = before with { Transform = context.TranslateGizmos.ProxyTransform };
            if (after != before)
                context.Commands.Submit(new TranslateCommand(before, after));
        }
        finally
        {
            // A command that throws must not leave the preview, the proxy, or this tool mid-drag.
            CleanUp(context, before.Id);
        }
    }

    // The one place a translate interaction is torn down. Lifecycle transitions cancel through the
    // registry rather than reaching for the coordinator or previews themselves.
    public void Cancel(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null)
            return;

        CleanUp(context, _before.Id);
    }

    private void CleanUp(IEditorToolContext context, MeshObjectId id)
    {
        _dragging = false;
        _before = null;
        _sourceCamera = null;

        try
        {
            context.Preview.ClearTransform(id);
        }
        finally
        {
            // Restores the proxy from whatever the document and previews now say, which is the
            // committed transform after a successful move and the original one after a cancel.
            context.TranslateGizmos.CancelAndSynchronize();
        }
    }
}
