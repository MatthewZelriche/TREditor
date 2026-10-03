using System;
using System.Collections.Generic;
using Godot;

// Moves the selected objects with the viewport gizmos. The tool owns the three-phase flow only: the
// coordinator owns the gizmo nodes, previews carry the drag feedback, and the document changes once,
// on mouse up, through a single command that replaces the MeshObject records captured at drag start.
public sealed class TranslateTool : ToolBase
{
    public const string ToolId = "tool.translate";

    private bool _dragging;
    private MeshObject[]? _before;
    private Transform3D _gizmoStart;
    private Camera3D? _sourceCamera;

    public TranslateTool(SelectionModeState? selectionMode = null)
        : base(selectionMode)
    {
    }

    public override string Id => ToolId;

    // Classifying the press as a gizmo grab before selection keeps a selection click from starting
    // a translation of the object it just selected.
    protected override void OnPreMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        if (_dragging)
            Cancel(context);
    }

    protected override bool TryBeginInteraction(IEditorToolContext context, EditorMouseInput input)
    {
        SelectionValue beforeSelection = context.Selection.Current;
        if (
            !input.IsShiftPressed
            && TryCaptureSelectedObjects(context, beforeSelection.ObjectIds, out MeshObject[] selected)
            && context.TranslateGizmos.BeginInteraction(input.Camera!, input.ScreenPosition)
        )
        {
            _dragging = true;
            _before = selected;
            _gizmoStart = context.TranslateGizmos.ProxyTransform;
            _sourceCamera = input.Camera;
            return true;
        }

        return false;
    }

    public override void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        if (!context.TranslateGizmos.UpdateInteraction(input.Camera, input.ScreenPosition))
            return;

        // The proxy is the only thing the gizmo moved. Apply its translation delta to every selected
        // object as preview state while leaving the document untouched until mouse up.
        Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - _gizmoStart.Origin;
        foreach (MeshObject before in _before)
        {
            context.Preview.SetTransform(
                before.Id,
                new Transform3D(before.Transform.Basis, before.Transform.Origin + delta)
            );
        }
    }

    public override void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        MeshObject[] before = _before;
        try
        {
            context.TranslateGizmos.EndInteraction();
            Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - _gizmoStart.Origin;
            if (!delta.IsZeroApprox())
            {
                var after = new MeshObject[before.Length];
                for (int index = 0; index < before.Length; index++)
                {
                    MeshObject meshObject = before[index];
                    after[index] = meshObject with
                    {
                        Transform = new Transform3D(
                            meshObject.Transform.Basis,
                            meshObject.Transform.Origin + delta
                        ),
                    };
                }

                context.Commands.Submit(new TranslateCommand(before, after));
            }
        }
        finally
        {
            // A command that throws must not leave the preview, the proxy, or this tool mid-drag.
            CleanUp(context);
        }
    }

    // The one place a translate interaction is torn down. Lifecycle transitions cancel through the
    // registry rather than reaching for the coordinator or previews themselves.
    protected override void OnCancel(IEditorToolContext context)
    {
        if (!_dragging || _before is null)
            return;

        CleanUp(context);
    }

    private void CleanUp(IEditorToolContext context)
    {
        MeshObject[] before = _before!;
        _dragging = false;
        _before = null;
        _sourceCamera = null;

        try
        {
            foreach (MeshObject meshObject in before)
                context.Preview.ClearTransform(meshObject.Id);
        }
        finally
        {
            // Restores the proxy from whatever the document and previews now say, which is the
            // committed transform after a successful move and the original one after a cancel.
            context.TranslateGizmos.CancelAndSynchronize();
        }
    }

    private static bool TryCaptureSelectedObjects(
        IEditorToolContext context,
        IReadOnlySet<MeshObjectId> selectedIds,
        out MeshObject[] selected
    )
    {
        if (selectedIds.Count == 0)
        {
            selected = [];
            return false;
        }

        selected = new MeshObject[selectedIds.Count];
        int index = 0;
        foreach (MeshObjectId id in selectedIds)
        {
            if (!context.Document.TryGetObject(id, out MeshObject meshObject))
            {
                selected = [];
                return false;
            }

            selected[index++] = meshObject;
        }

        return true;
    }
}
